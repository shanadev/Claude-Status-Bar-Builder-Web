// Claude Status Bar Builder — a visual designer for Claude Code status lines.
// Copyright (C) 2026 Stephen Shanafelt
// SPDX-License-Identifier: GPL-3.0-only
//
// This program is free software: you can redistribute it and/or modify it under
// the terms of the GNU General Public License as published by the Free Software
// Foundation, version 3. It is distributed WITHOUT ANY WARRANTY; see the full
// license text in the LICENSE file at the root of this repository.

namespace StatusBar.Core;

/// <summary>Value formatting helpers shared by all elements.</summary>
public static class Fmt
{
    /// <summary>15234 → "15.2k", 1200000 → "1.2M".</summary>
    public static string Compact(double n)
    {
        if (n >= 1_000_000) return (n / 1_000_000).ToString("0.#") + "M";
        if (n >= 1_000) return (n / 1_000).ToString("0.#") + "k";
        return ((long)n).ToString();
    }

    public static string Usd(double v, int decimals) => "$" + v.ToString("F" + decimals);

    public static string DurationMs(double ms, string? style)
    {
        var t = TimeSpan.FromMilliseconds(Math.Max(0, ms));
        return style switch
        {
            "colons" => t.TotalHours >= 1
                ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
                : $"{t.Minutes}:{t.Seconds:00}",
            "minsec" => $"{(int)t.TotalMinutes}m {t.Seconds}s",
            _ => t.TotalHours >= 1 ? $"{(int)t.TotalHours}hr {t.Minutes}m" : $"{t.Minutes}m {t.Seconds}s",
        };
    }

    public static string Countdown(long epochSeconds, DateTimeOffset now)
    {
        var span = DateTimeOffset.FromUnixTimeSeconds(epochSeconds) - now;
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;
        return span.TotalHours >= 1 ? $"{(int)span.TotalHours}hr {span.Minutes}m" : $"{span.Minutes}m";
    }

    /// <summary>Epoch → "2pm", or "fri,3am" with the weekday.</summary>
    public static string TimeAt(long epochSeconds, bool withWeekday)
    {
        var local = DateTimeOffset.FromUnixTimeSeconds(epochSeconds).ToLocalTime();
        string t = local.ToString("htt").ToLowerInvariant();
        return withWeekday ? local.ToString("ddd").ToLowerInvariant() + "," + t : t;
    }

    public static string Clock(DateTimeOffset now, string? style) => style switch
    {
        "24h" => now.ToString("HH:mm"),
        "12h+sec" => now.ToString("h:mm:sstt").ToLowerInvariant(),
        _ => now.ToString("h:mmtt").ToLowerInvariant(),
    };

    public static string Dir(string? path, string? style)
    {
        if (string.IsNullOrEmpty(path)) return "";
        string norm = path.Replace('\\', '/').TrimEnd('/');
        return style switch
        {
            "full" => norm,
            "short" => ShortenPath(norm),
            _ => norm[(norm.LastIndexOf('/') + 1)..],
        };
    }

    static string ShortenPath(string norm)
    {
        var parts = norm.Split('/');
        return parts.Length <= 2 ? norm : parts[0] + "/…/" + parts[^1];
    }

    public static string Percent(double v, string? fmt) =>
        fmt == "1dp" ? v.ToString("0.0") + "%" : Math.Round(v).ToString("0") + "%";

    /// <summary>Display-cell width, from the same Unicode 11 tables the preview terminal uses
    /// (UnicodeWidth) — ZWJ/variation selectors are width 0 there, so no special-casing.</summary>
    public static int Width(string s)
    {
        int w = 0;
        foreach (var rune in s.EnumerateRunes())
            w += UnicodeWidth.Of(rune.Value);
        return w;
    }
}
