// Claude Status Bar Builder — a visual designer for Claude Code status lines.
// Copyright (C) 2026 Stephen Shanafelt
// SPDX-License-Identifier: GPL-3.0-only
//
// This program is free software: you can redistribute it and/or modify it under
// the terms of the GNU General Public License as published by the Free Software
// Foundation, version 3. It is distributed WITHOUT ANY WARRANTY; see the full
// license text in the LICENSE file at the root of this repository.

namespace StatusBar.Core;

/// <summary>A fragment of styled text. Null colors inherit the segment defaults.</summary>
public sealed record StyledRun(
    string Text,
    string? Fg = null,
    string? Bg = null,
    bool Bold = false,
    bool Dim = false,
    bool Italic = false,
    bool Underline = false);

/// <summary>
/// ANSI escape-code helpers. Color specs are strings:
/// "#RRGGBB" (truecolor), "ansi:0".."ansi:255" (palette), null/""/"none" (terminal default).
/// </summary>
public static class Ansi
{
    public const string Reset = "\u001b[0m";

    /// <summary>The standard 16-color palette (Windows Terminal defaults) for pickers and RGB math.</summary>
    public static readonly (string Name, string Hex)[] Ansi16 =
    {
        ("black", "#0c0c0c"), ("red", "#c50f1f"), ("green", "#13a10e"), ("yellow", "#c19c00"),
        ("blue", "#0037da"), ("magenta", "#881798"), ("cyan", "#3a96dd"), ("white", "#cccccc"),
        ("bright black", "#767676"), ("bright red", "#e74856"), ("bright green", "#16c60c"), ("bright yellow", "#f9f1a5"),
        ("bright blue", "#3b78ff"), ("bright magenta", "#b4009e"), ("bright cyan", "#61d6d6"), ("bright white", "#f2f2f2"),
    };

    public static bool IsSet(string? spec) => !string.IsNullOrWhiteSpace(spec) && spec.Trim() != "none";

    public static bool TryParseHex(string? spec, out byte r, out byte g, out byte b)
    {
        r = g = b = 0;
        if (spec is null) return false;
        spec = spec.Trim();
        if (spec.Length != 7 || spec[0] != '#') return false;
        try
        {
            r = Convert.ToByte(spec.Substring(1, 2), 16);
            g = Convert.ToByte(spec.Substring(3, 2), 16);
            b = Convert.ToByte(spec.Substring(5, 2), 16);
            return true;
        }
        catch { return false; }
    }

    static string? Code(string? spec, bool bg)
    {
        if (!IsSet(spec)) return null;
        spec = spec!.Trim();
        if (spec.StartsWith("ansi:", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(spec.AsSpan(5), out int n) || n < 0 || n > 255) return null;
            if (n < 8) return ((bg ? 40 : 30) + n).ToString();
            if (n < 16) return ((bg ? 100 : 90) + n - 8).ToString();
            return (bg ? "48;5;" : "38;5;") + n;
        }
        if (TryParseHex(spec, out byte r, out byte g, out byte b))
            return (bg ? "48;2;" : "38;2;") + $"{r};{g};{b}";
        return null;
    }

    /// <summary>Builds an SGR escape sequence, or "" if nothing to set.</summary>
    public static string Sgr(string? fg, string? bg, bool bold = false, bool dim = false, bool italic = false, bool underline = false)
    {
        var codes = new List<string>(4);
        if (bold) codes.Add("1");
        if (dim) codes.Add("2");
        if (italic) codes.Add("3");
        if (underline) codes.Add("4");
        if (Code(fg, false) is string f) codes.Add(f);
        if (Code(bg, true) is string b) codes.Add(b);
        return codes.Count == 0 ? "" : "\u001b[" + string.Join(';', codes) + "m";
    }

    /// <summary>Resolves any color spec to an RGB hex (for gradient math). Returns null for none/default.</summary>
    public static string? ToHex(string? spec)
    {
        if (!IsSet(spec)) return null;
        spec = spec!.Trim();
        if (TryParseHex(spec, out _, out _, out _)) return spec;
        if (spec.StartsWith("ansi:", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(spec.AsSpan(5), out int n) && n >= 0 && n < 16)
            return Ansi16[n].Hex;
        return null;
    }

    /// <summary>Linear interpolation between two hex colors, t in [0,1].</summary>
    public static string Lerp(string fromHex, string toHex, double t)
    {
        if (!TryParseHex(fromHex, out byte r1, out byte g1, out byte b1)) return fromHex;
        if (!TryParseHex(toHex, out byte r2, out byte g2, out byte b2)) return fromHex;
        t = Math.Clamp(t, 0, 1);
        byte r = (byte)Math.Round(r1 + (r2 - r1) * t);
        byte g = (byte)Math.Round(g1 + (g2 - g1) * t);
        byte b = (byte)Math.Round(b1 + (b2 - b1) * t);
        return $"#{r:x2}{g:x2}{b:x2}";
    }
}
