// Hack the Claude Status Bar — template review console (maintainer tool).
// Copyright (C) 2026 Stephen Shanafelt
// SPDX-License-Identifier: GPL-3.0-only
//
// This program is free software: you can redistribute it and/or modify it under
// the terms of the GNU General Public License as published by the Free Software
// Foundation, version 3. It is distributed WITHOUT ANY WARRANTY; see the full
// license text in the LICENSE file at the root of this repository.
//
// Drains the site's template-submission queue: fetches pending entries, renders
// each bar live in the console (same Composer + sample data the site uses),
// asks yea or nay, appends approved bars to wwwroot/templates.json, offers a
// commit+push (Railway redeploys = published), then clears the processed
// entries server-side. Skipped entries stay queued; quitting loses nothing.
//
//   dotnet run --project tools/StatusBar.Review [-- --site <url>]
//   token: SUBMIT_ADMIN_TOKEN env var (prompted for if unset)

using System.Diagnostics;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StatusBar.Core;

const string DefaultSite = "https://claude-status-bar-builder-web-production.up.railway.app/";

Console.OutputEncoding = Encoding.UTF8;
Native.TryEnableVt();

var site = DefaultSite;
for (int i = 0; i < args.Length - 1; i++)
    if (args[i] == "--site") site = args[i + 1];
if (!site.EndsWith('/')) site += "/";

// templates.json lives in this repo — find the root from wherever we were run.
var repoRoot = FindRepoRoot();
if (repoRoot is null)
{
    Fail("can't find StatusBarBuilder.slnx above the current directory — run from inside the repo.");
    return 1;
}
var templatesPath = Path.Combine(repoRoot, "src", "StatusBar.Web", "wwwroot", "templates.json");

var token = Environment.GetEnvironmentVariable("SUBMIT_ADMIN_TOKEN");
if (string.IsNullOrWhiteSpace(token))
    token = PromptSecret("admin token (SUBMIT_ADMIN_TOKEN): ");
if (string.IsNullOrWhiteSpace(token))
{
    Fail("no token, no gate.");
    return 1;
}

using var http = new HttpClient { BaseAddress = new Uri(site), Timeout = TimeSpan.FromSeconds(30) };
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

Info($"dialing {site} …");
JsonNode? queue;
try
{
    var resp = await http.GetAsync("api/admin/submissions");
    if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
    {
        Fail("401 — that token doesn't open this door.");
        return 1;
    }
    resp.EnsureSuccessStatusCode();
    queue = JsonNode.Parse(await resp.Content.ReadAsStringAsync());
}
catch (Exception ex)
{
    Fail($"couldn't reach the queue: {ex.Message}");
    return 1;
}

var entries = queue?["entries"]?.AsArray() ?? [];
if (entries.Count == 0)
{
    Info("queue is empty — nothing to judge today.");
    return 0;
}
Info($"{entries.Count} submission(s) pending.\n");

// Sample context once; render at the console's width like a real terminal would.
var ctx = new SampleData().Build();
ctx.Computed.Columns = Math.Clamp(SafeWindowWidth() - 2, 40, 100);

var approved = new List<JsonObject>();
var processedIds = new List<string>();
bool quit = false;

foreach (var node in entries)
{
    if (quit) break;
    if (node is null) continue;
    var id = (string?)node["id"] ?? "";
    var name = (string?)node["name"] ?? "(unnamed)";
    var author = (string?)node["author"] ?? "anonymous";
    var date = (string?)node["date"] ?? "";
    var description = (string?)node["description"] ?? "";
    var themeNode = node["theme"];

    Console.WriteLine($"\x1b[36m── {name}\x1b[0m  \x1b[90mby {author} · {date} · {id}\x1b[0m");
    if (description.Length > 0) Console.WriteLine($"\x1b[90m   {description}\x1b[0m");

    Theme? theme = null;
    if (themeNode is not null)
        try { theme = Theme.FromJson(themeNode.ToJsonString()); } catch { /* judged below */ }

    if (theme is null || theme.Rows.Count == 0)
    {
        Console.WriteLine("\x1b[31m   payload doesn't decode to a status bar — reject is the only sane option\x1b[0m");
    }
    else
    {
        Console.WriteLine();
        foreach (var line in Composer.Compose(theme, ctx))
            if (line.Length > 0)
                Console.WriteLine("  " + line + "\x1b[0m");
        Console.WriteLine();
    }

    switch (Ask("   [y] approve · [n] reject · [s] skip · [q] quit: ", "ynsq"))
    {
        case 'y' when theme is not null && theme.Rows.Count > 0:
            var entry = new JsonObject
            {
                ["name"] = name,
                ["author"] = author,
                ["date"] = date,
            };
            if (description.Length > 0) entry["description"] = description;
            entry["theme"] = JsonNode.Parse(themeNode!.ToJsonString());
            approved.Add(entry);
            processedIds.Add(id);
            Console.WriteLine("\x1b[32m   approved.\x1b[0m\n");
            break;
        case 'y':
            Console.WriteLine("\x1b[31m   can't approve an undecodable bar — leaving it queued.\x1b[0m\n");
            break;
        case 'n':
            processedIds.Add(id);
            Console.WriteLine("\x1b[31m   rejected.\x1b[0m\n");
            break;
        case 's':
            Console.WriteLine("\x1b[90m   skipped — stays queued.\x1b[0m\n");
            break;
        case 'q':
            quit = true;
            Console.WriteLine("\x1b[90m   quitting — everything unjudged stays queued.\x1b[0m\n");
            break;
    }
}

// 1. Land approved bars in templates.json (local file first — nothing is lost
//    even if the network dies right after).
if (approved.Count > 0)
{
    var doc = JsonNode.Parse(await File.ReadAllTextAsync(templatesPath))
        ?? throw new InvalidOperationException("templates.json parsed to null");
    var arr = doc["templates"]?.AsArray()
        ?? throw new InvalidOperationException("templates.json has no templates array");
    foreach (var entry in approved) arr.Add(entry);
    await File.WriteAllTextAsync(templatesPath,
        doc.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
    Info($"{approved.Count} bar(s) appended to {Path.GetRelativePath(repoRoot, templatesPath)}");
}

// 2. Clear processed entries from the server queue (approved + rejected; skips stay).
if (processedIds.Count > 0)
{
    try
    {
        var resp = await http.PostAsJsonAsync2("api/admin/clear", new { ids = processedIds });
        resp.EnsureSuccessStatusCode();
        Info($"cleared {processedIds.Count} processed entr(y/ies) from the server queue.");
    }
    catch (Exception ex)
    {
        Warn($"couldn't clear the server queue ({ex.Message}) — processed entries will show up again next run.");
    }
}

// 3. Publish: a push to main makes Railway redeploy with the new templates.json.
if (approved.Count > 0)
{
    if (Ask("publish now? commit + push [y/n]: ", "yn") == 'y')
    {
        var names = string.Join(", ", approved.Select(a => (string?)a["name"]));
        int rc = Git(repoRoot, "add", Path.GetRelativePath(repoRoot, templatesPath).Replace('\\', '/'));
        if (rc == 0) rc = Git(repoRoot, "commit", "-m", $"Add template(s): {names}");
        if (rc == 0) rc = Git(repoRoot, "push");
        if (rc == 0) Info("pushed — Railway deploys it from here. HACK THE PLANET.");
        else Warn("git reported a problem above — templates.json is staged locally, finish by hand.");
    }
    else
    {
        Info("not published — commit and push src/StatusBar.Web/wwwroot/templates.json when ready.");
    }
}

return 0;

// ── helpers ─────────────────────────────────────────────────────────────────

static string? FindRepoRoot()
{
    var dir = new DirectoryInfo(Environment.CurrentDirectory);
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "StatusBarBuilder.slnx"))) return dir.FullName;
        dir = dir.Parent;
    }
    return null;
}

static int SafeWindowWidth()
{
    try { return Console.WindowWidth; } catch { return 80; }
}

static char Ask(string prompt, string allowed)
{
    Console.Write(prompt);
    if (Console.IsInputRedirected)
    {
        // Piped input (tests/scripts): one answer per line.
        while (true)
        {
            var line = Console.In.ReadLine();
            if (line is null) { Console.WriteLine("q"); return 'q'; } // EOF = bail safely
            var c = char.ToLowerInvariant(line.Trim().FirstOrDefault());
            if (allowed.Contains(c)) { Console.WriteLine(c); return c; }
        }
    }
    while (true)
    {
        var k = char.ToLowerInvariant(Console.ReadKey(intercept: true).KeyChar);
        if (allowed.Contains(k))
        {
            Console.WriteLine(k);
            return k;
        }
    }
}

static string PromptSecret(string label)
{
    Console.Write(label);
    if (Console.IsInputRedirected) return Console.In.ReadLine()?.Trim() ?? "";
    var sb = new StringBuilder();
    while (true)
    {
        var k = Console.ReadKey(intercept: true);
        if (k.Key == ConsoleKey.Enter) { Console.WriteLine(); return sb.ToString(); }
        if (k.Key == ConsoleKey.Backspace) { if (sb.Length > 0) sb.Length--; continue; }
        if (!char.IsControl(k.KeyChar)) sb.Append(k.KeyChar);
    }
}

static int Git(string repo, params string[] args)
{
    var psi = new ProcessStartInfo("git") { WorkingDirectory = repo };
    psi.ArgumentList.Add("-C");
    psi.ArgumentList.Add(repo);
    foreach (var a in args) psi.ArgumentList.Add(a);
    using var p = Process.Start(psi)!;
    p.WaitForExit();
    return p.ExitCode;
}

static void Info(string msg) => Console.WriteLine($"\x1b[36m{msg}\x1b[0m");
static void Warn(string msg) => Console.WriteLine($"\x1b[33m{msg}\x1b[0m");
static void Fail(string msg) => Console.WriteLine($"\x1b[31m{msg}\x1b[0m");

static class Native
{
    [DllImport("kernel32.dll")] static extern IntPtr GetStdHandle(int nStdHandle);
    [DllImport("kernel32.dll")] static extern bool GetConsoleMode(IntPtr h, out uint mode);
    [DllImport("kernel32.dll")] static extern bool SetConsoleMode(IntPtr h, uint mode);

    /// <summary>Classic conhost needs ENABLE_VIRTUAL_TERMINAL_PROCESSING for ANSI output.</summary>
    public static void TryEnableVt()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            var h = GetStdHandle(-11); // STD_OUTPUT_HANDLE
            if (GetConsoleMode(h, out var mode)) SetConsoleMode(h, mode | 0x0004);
        }
        catch { /* worst case: escape codes print literally */ }
    }
}

static class HttpJsonExtensions
{
    /// <summary>PostAsJsonAsync without dragging in System.Net.Http.Json's default options.</summary>
    public static Task<HttpResponseMessage> PostAsJsonAsync2(this HttpClient http, string url, object body) =>
        http.PostAsync(url, new StringContent(
            JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            Encoding.UTF8, "application/json"));
}
