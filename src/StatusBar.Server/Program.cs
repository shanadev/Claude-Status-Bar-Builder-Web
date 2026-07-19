// Hack the Claude Status Bar — host + template submission API.
// Copyright (C) 2026 Stephen Shanafelt
// SPDX-License-Identifier: GPL-3.0-only
//
// This program is free software: you can redistribute it and/or modify it under
// the terms of the GNU General Public License as published by the Free Software
// Foundation, version 3. It is distributed WITHOUT ANY WARRANTY; see the full
// license text in the LICENSE file at the root of this repository.
//
// Serves the Blazor WASM site (replacing the old nginx container) and adds the
// template-submission queue: POST /api/submit appends to a JSONL file that a
// maintainer drains with tools/StatusBar.Review. The queue file lives on a
// Railway volume (SUBMISSIONS_PATH, default /data/submissions.jsonl there;
// falls back to ./data/submissions.jsonl for local runs).

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.StaticFiles;
using StatusBar.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Railway injects PORT at runtime; local runs use launchSettings (5153).
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Every POST this server accepts is tiny; keep the door narrow.
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 64 * 1024);

builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        ["application/wasm", "font/ttf", "image/svg+xml"]);
});

var app = builder.Build();

// Railway's edge proxy terminates TLS and forwards the client IP.
var fwd = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
};
fwd.KnownIPNetworks.Clear();
fwd.KnownProxies.Clear();
app.UseForwardedHeaders(fwd);

app.UseResponseCompression();

// Everything under _framework/ is content-fingerprinted by the publish.
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path.StartsWithSegments("/_framework"))
        ctx.Response.OnStarting(() =>
        {
            ctx.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            return Task.CompletedTask;
        });
    await next();
});

app.UseBlazorFrameworkFiles();

// The install scripts are fetched with `irm | iex` / `curl | bash`. Without a
// text type + charset, Invoke-RestMethod can return byte[] (pwsh) or mis-decode
// UTF-8 as Latin-1 (PS 5.1) — same fix the old nginx config carried.
var contentTypes = new FileExtensionContentTypeProvider();
contentTypes.Mappings[".ps1"] = "text/plain; charset=utf-8";
contentTypes.Mappings[".sh"] = "text/plain; charset=utf-8";
var staticOptions = new StaticFileOptions
{
    ContentTypeProvider = contentTypes,
    OnPrepareResponse = ctx =>
    {
        // index.html carries the importmap of current fingerprints; the gallery
        // re-fetches templates.json after every approval merge — never cache.
        if (ctx.File.Name is "index.html" or "templates.json")
            ctx.Context.Response.Headers.CacheControl = "no-cache";
    },
};
app.UseStaticFiles(staticOptions);

// ── Template submission queue ────────────────────────────────────────────────

var submissionsPath = Environment.GetEnvironmentVariable("SUBMISSIONS_PATH")
    ?? Path.Combine(app.Environment.ContentRootPath, "data", "submissions.jsonl");
var adminToken = Environment.GetEnvironmentVariable("SUBMIT_ADMIN_TOKEN");
var jsonOpts = new JsonSerializerOptions(JsonSerializerDefaults.Web);
var fileLock = new SemaphoreSlim(1, 1);
const long QueueCapBytes = 256 * 1024;

// Per-IP submit throttle; moderation catches junk, this just keeps floods out.
var submitLog = new ConcurrentDictionary<string, List<DateTimeOffset>>();
bool AllowSubmit(string ip)
{
    if (submitLog.Count > 10_000) submitLog.Clear();
    var now = DateTimeOffset.UtcNow;
    var times = submitLog.GetOrAdd(ip, _ => []);
    lock (times)
    {
        times.RemoveAll(t => now - t > TimeSpan.FromHours(1));
        if (times.Count >= 5) return false;
        times.Add(now);
        return true;
    }
}

bool AdminOk(HttpRequest req)
{
    if (string.IsNullOrEmpty(adminToken)) return false;
    var auth = req.Headers.Authorization.ToString();
    if (!auth.StartsWith("Bearer ", StringComparison.Ordinal)) return false;
    var provided = Encoding.UTF8.GetBytes(auth["Bearer ".Length..].Trim());
    var expected = Encoding.UTF8.GetBytes(adminToken);
    return provided.Length == expected.Length
        && CryptographicOperations.FixedTimeEquals(provided, expected);
}

app.MapPost("/api/submit", async (SubmissionRequest req, HttpContext http) =>
{
    var ip = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    if (!AllowSubmit(ip))
        return Results.Json(new { error = "easy there — try again in an hour" }, statusCode: 429);

    var name = (req.Name ?? "").Trim();
    var author = (req.Author ?? "").Trim();
    var description = (req.Description ?? "").Trim();
    var t = (req.T ?? "").Trim();
    if (name.Length is 0 or > 60) return Results.BadRequest(new { error = "name: 1–60 characters" });
    if (author.Length is 0 or > 60) return Results.BadRequest(new { error = "author: 1–60 characters" });
    if (description.Length > 255) return Results.BadRequest(new { error = "description: max 255 characters" });
    if (t.Length is 0 or > 10_000 || ThemeCodec.TryDecode(t) is null)
        return Results.BadRequest(new { error = "that share payload doesn't decode to a status bar" });

    var entry = new StoredSubmission(
        Id: $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(2))}",
        Name: name, Author: author, Description: description,
        Date: DateTime.UtcNow.ToString("yyyy-MM-dd"), T: t);

    await fileLock.WaitAsync();
    try
    {
        var file = new FileInfo(submissionsPath);
        if (file.Exists && file.Length > QueueCapBytes)
            return Results.Json(new { error = "review queue is full — try again later" }, statusCode: 503);
        Directory.CreateDirectory(file.DirectoryName!);
        await File.AppendAllTextAsync(submissionsPath,
            JsonSerializer.Serialize(entry, jsonOpts) + "\n");
    }
    finally { fileLock.Release(); }

    return Results.Ok(new { ok = true });
});

app.MapGet("/api/admin/submissions", async (HttpRequest req) =>
{
    if (!AdminOk(req)) return Results.Unauthorized();

    await fileLock.WaitAsync();
    string[] lines;
    try
    {
        lines = File.Exists(submissionsPath)
            ? await File.ReadAllLinesAsync(submissionsPath) : [];
    }
    finally { fileLock.Release(); }

    var entries = new List<JsonNode>();
    foreach (var line in lines)
    {
        if (string.IsNullOrWhiteSpace(line)) continue;
        StoredSubmission? s;
        try { s = JsonSerializer.Deserialize<StoredSubmission>(line, jsonOpts); }
        catch { continue; } // a corrupt line shouldn't wedge the whole queue
        if (s is null) continue;
        // Hand the reviewer ready-to-use theme JSON alongside the raw payload.
        var theme = ThemeCodec.TryDecode(s.T);
        entries.Add(new JsonObject
        {
            ["id"] = s.Id, ["name"] = s.Name, ["author"] = s.Author,
            ["description"] = s.Description, ["date"] = s.Date, ["t"] = s.T,
            ["theme"] = theme is null ? null : JsonNode.Parse(theme.ToJson()),
        });
    }
    return Results.Json(new JsonObject { ["entries"] = new JsonArray([.. entries]) });
});

app.MapPost("/api/admin/clear", async (ClearRequest req, HttpContext http) =>
{
    if (!AdminOk(http.Request)) return Results.Unauthorized();
    var ids = (req.Ids ?? []).ToHashSet(StringComparer.Ordinal);

    await fileLock.WaitAsync();
    try
    {
        if (!File.Exists(submissionsPath)) return Results.Ok(new { removed = 0, remaining = 0 });
        var lines = await File.ReadAllLinesAsync(submissionsPath);
        var kept = lines.Where(l =>
        {
            if (string.IsNullOrWhiteSpace(l)) return false;
            try
            {
                var s = JsonSerializer.Deserialize<StoredSubmission>(l, jsonOpts);
                return s is not null && !ids.Contains(s.Id);
            }
            catch { return false; }
        }).ToArray();
        // Rewrite atomically so a crash mid-write can't corrupt the queue.
        var tmp = submissionsPath + ".tmp";
        await File.WriteAllTextAsync(tmp, kept.Length == 0 ? "" : string.Join("\n", kept) + "\n");
        File.Move(tmp, submissionsPath, overwrite: true);
        return Results.Ok(new { removed = lines.Length - kept.Length, remaining = kept.Length });
    }
    finally { fileLock.Release(); }
});

app.MapGet("/healthz", () => Results.Text("ok"));

// SPA fallback: /build, /export, /templates and share links resolve client-side.
app.MapFallbackToFile("index.html", staticOptions);

app.Run();

sealed record SubmissionRequest(string? Name, string? Author, string? Description, string? T);
sealed record StoredSubmission(string Id, string Name, string Author, string Description, string Date, string T);
sealed record ClearRequest(string[]? Ids);
