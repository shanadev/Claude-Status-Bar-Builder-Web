// Hack the Claude Status Bar — theme <-> URL fragment codec.
// Copyright (C) 2026 Stephen Shanafelt
// SPDX-License-Identifier: GPL-3.0-only
//
// This program is free software: you can redistribute it and/or modify it under
// the terms of the GNU General Public License as published by the Free Software
// Foundation, version 3. It is distributed WITHOUT ANY WARRANTY; see the full
// license text in the LICENSE file at the root of this repository.

using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using StatusBar.Core;

namespace StatusBar.Web.Services;

/// <summary>
/// The whole theme compressed into the URL fragment: "#t=1.&lt;base64url(deflate(json))&gt;".
/// The "1." version prefix keeps the scheme forward-compatible — a future format bumps the
/// number and old payloads keep decoding. No backend, no accounts: the link IS the save file.
/// </summary>
public static class ThemeCodec
{
    // Compact JSON for the URL (Theme.JsonOpts indents for on-disk readability).
    static readonly JsonSerializerOptions Compact = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Encode(Theme theme)
    {
        var json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(theme, Compact));
        using var buffer = new MemoryStream();
        using (var deflate = new DeflateStream(buffer, CompressionLevel.SmallestSize, leaveOpen: true))
            deflate.Write(json);
        return "1." + Convert.ToBase64String(buffer.ToArray())
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    /// <summary>Decodes a "#t=" payload (with or without the version prefix trimmed of "t=").</summary>
    public static Theme? TryDecode(string payload)
    {
        try
        {
            payload = Uri.UnescapeDataString(payload).Trim();
            int dot = payload.IndexOf('.');
            if (dot < 0) return null;
            if (payload[..dot] != "1") return null; // unknown future version

            var b64 = payload[(dot + 1)..].Replace('-', '+').Replace('_', '/');
            b64 = b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '=');
            using var input = new MemoryStream(Convert.FromBase64String(b64));
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);
            using var reader = new StreamReader(deflate, Encoding.UTF8);
            var theme = JsonSerializer.Deserialize<Theme>(reader.ReadToEnd(), Theme.JsonOpts);
            return theme is { Rows.Count: > 0 } ? theme : null;
        }
        catch { return null; }
    }

    /// <summary>Plain base64 of the theme JSON — what the install one-liners embed via SBB_THEME.</summary>
    public static string ToBase64Json(Theme theme) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(theme, Compact)));
}
