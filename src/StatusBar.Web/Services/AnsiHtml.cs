// Hack the Claude Status Bar — ANSI → HTML converter for static gallery previews.
// Copyright (C) 2026 Stephen Shanafelt
// SPDX-License-Identifier: GPL-3.0-only
//
// This program is free software: you can redistribute it and/or modify it under
// the terms of the GNU General Public License as published by the Free Software
// Foundation, version 3. It is distributed WITHOUT ANY WARRANTY; see the full
// license text in the LICENSE file at the root of this repository.

using System.Text;
using StatusBar.Core;

namespace StatusBar.Web.Services;

/// <summary>
/// Renders composed ANSI rows as HTML spans, so template cards can show a faithful
/// preview without mounting an xterm instance per card. Handles exactly the SGR
/// vocabulary <see cref="Ansi.Sgr"/> emits (reset, bold/dim/italic/underline,
/// 16-color, 256-color, truecolor).
/// </summary>
public static class AnsiHtml
{
    public static string RowsToHtml(IEnumerable<string> rows)
    {
        var sb = new StringBuilder();
        foreach (var row in rows)
        {
            if (row.Length == 0) continue; // all-hidden row — the renderer drops it too
            sb.Append("<div class=\"ansiline\">");
            AppendRow(sb, row);
            sb.Append("</div>");
        }
        return sb.ToString();
    }

    static void AppendRow(StringBuilder sb, string row)
    {
        string? fg = null, bg = null;
        bool bold = false, dim = false, italic = false, underline = false;
        var text = new StringBuilder();

        void FlushRun()
        {
            if (text.Length == 0) return;
            sb.Append("<span style=\"");
            if (fg is not null) sb.Append("color:").Append(fg).Append(';');
            if (bg is not null) sb.Append("background:").Append(bg).Append(';');
            if (bold) sb.Append("font-weight:700;");
            if (dim) sb.Append("opacity:.6;");
            if (italic) sb.Append("font-style:italic;");
            if (underline) sb.Append("text-decoration:underline;");
            sb.Append("\">").Append(System.Net.WebUtility.HtmlEncode(text.ToString())).Append("</span>");
            text.Clear();
        }

        void Apply(string sgrParams)
        {
            var p = sgrParams.Length == 0 ? new[] { "0" } : sgrParams.Split(';');
            for (int k = 0; k < p.Length; k++)
            {
                switch (p[k])
                {
                    case "0": fg = bg = null; bold = dim = italic = underline = false; break;
                    case "1": bold = true; break;
                    case "2": dim = true; break;
                    case "3": italic = true; break;
                    case "4": underline = true; break;
                    case "38" or "48":
                        bool isBg = p[k] == "48";
                        string? hex = null;
                        if (k + 4 < p.Length && p[k + 1] == "2")
                        {
                            hex = $"#{Num(p[k + 2]):x2}{Num(p[k + 3]):x2}{Num(p[k + 4]):x2}";
                            k += 4;
                        }
                        else if (k + 2 < p.Length && p[k + 1] == "5")
                        {
                            hex = Indexed(Num(p[k + 2]));
                            k += 2;
                        }
                        if (isBg) bg = hex; else fg = hex;
                        break;
                    default:
                        if (int.TryParse(p[k], out int n))
                        {
                            if (n is >= 30 and <= 37) fg = Ansi.Ansi16[n - 30].Hex;
                            else if (n is >= 90 and <= 97) fg = Ansi.Ansi16[n - 90 + 8].Hex;
                            else if (n is >= 40 and <= 47) bg = Ansi.Ansi16[n - 40].Hex;
                            else if (n is >= 100 and <= 107) bg = Ansi.Ansi16[n - 100 + 8].Hex;
                        }
                        break;
                }
            }
        }

        for (int i = 0; i < row.Length; i++)
        {
            if (row[i] == '\u001b' && i + 1 < row.Length && row[i + 1] == '[')
            {
                int m = row.IndexOf('m', i + 2);
                if (m < 0) break; // truncated escape — nothing sane to render past it
                FlushRun();
                Apply(row[(i + 2)..m]);
                i = m;
                continue;
            }
            text.Append(row[i]);
        }
        FlushRun();
    }

    static int Num(string s) => int.TryParse(s, out int v) ? Math.Clamp(v, 0, 255) : 0;

    /// <summary>Standard xterm 256-color palette: 16 base + 6×6×6 cube + grayscale ramp.</summary>
    static string Indexed(int n)
    {
        if (n < 16) return Ansi.Ansi16[n].Hex;
        if (n < 232)
        {
            int v = n - 16;
            int r = v / 36, g = v / 6 % 6, b = v % 6;
            static int C(int x) => x == 0 ? 0 : 55 + 40 * x;
            return $"#{C(r):x2}{C(g):x2}{C(b):x2}";
        }
        int gray = 8 + 10 * (n - 232);
        return $"#{gray:x2}{gray:x2}{gray:x2}";
    }
}
