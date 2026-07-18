// Claude Status Bar Builder — a visual designer for Claude Code status lines.
// Copyright (C) 2026 Stephen Shanafelt
// SPDX-License-Identifier: GPL-3.0-only
//
// This program is free software: you can redistribute it and/or modify it under
// the terms of the GNU General Public License as published by the Free Software
// Foundation, version 3. It is distributed WITHOUT ANY WARRANTY; see the full
// license text in the LICENSE file at the root of this repository.

using System.Text;

namespace StatusBar.Core;

/// <summary>Turns theme rows into ANSI strings, handling separators, powerline color chaining, caps, and spacers.</summary>
public static class Composer
{
    static readonly Dictionary<SeparatorStyle, (string Glyph, bool Solid)> Seps = new()
    {
        { SeparatorStyle.None, ("", false) },
        { SeparatorStyle.Space, (" ", false) },
        { SeparatorStyle.Pipe, ("|", false) },
        { SeparatorStyle.Dot, ("·", false) },
        { SeparatorStyle.ChevronLine, ("", false) },
        { SeparatorStyle.Chevron, ("", true) },
        { SeparatorStyle.Round, ("", true) },
        { SeparatorStyle.Slant, ("", true) },
        { SeparatorStyle.Backslant, ("", true) },
        { SeparatorStyle.Flame, ("", true) },
        { SeparatorStyle.Blocks, ("", true) },
    };

    static readonly Dictionary<CapStyle, (string Left, string Right)> Caps = new()
    {
        { CapStyle.Chevron, ("", "") },
        { CapStyle.Round, ("", "") },
        { CapStyle.Slant, ("", "") },
        { CapStyle.Backslant, ("", "") },
        { CapStyle.Flame, ("", "") },
        { CapStyle.Blocks, ("", "") },
    };

    public static string[] Compose(Theme theme, RenderContext ctx) =>
        theme.Rows.Select(r => ComposeRow(r, ctx)).ToArray();

    public static string ComposeRow(Row row, RenderContext ctx)
    {
        // Split the row into chain blocks separated by spacers (spacers break the capsule chain).
        var blocks = new List<(bool IsSpacer, string Ansi, int Width, bool Flex)>();
        var chain = new List<RenderedSegment>();

        void Flush()
        {
            if (chain.Count == 0) return;
            var (ansi, width) = RenderChain(chain, row);
            blocks.Add((false, ansi, width, false));
            chain.Clear();
        }

        foreach (var seg in row.Segments)
        {
            if (seg.Element == ElementKind.Spacer)
            {
                Flush();
                bool flex = string.IsNullOrEmpty(seg.Text) || seg.Text!.Trim() == "flex";
                int w = !flex && int.TryParse(seg.Text, out int n) ? Math.Max(0, n) : 2;
                blocks.Add((true, "", flex ? 0 : w, flex));
            }
            else if (SegmentRenderer.Render(seg, ctx) is RenderedSegment rendered)
            {
                chain.Add(rendered);
            }
        }
        Flush();

        int fixedWidth = blocks.Sum(b => b.Width);
        int flexCount = blocks.Count(b => b.Flex);
        int flexEach = flexCount > 0 ? Math.Max(1, (ctx.Computed.Columns - fixedWidth) / flexCount) : 0;

        var sb = new StringBuilder();
        foreach (var b in blocks)
        {
            if (b.IsSpacer) sb.Append(new string(' ', b.Flex ? flexEach : b.Width));
            else sb.Append(b.Ansi);
        }
        sb.Append(Ansi.Reset);
        return sb.ToString();
    }

    static (string Ansi, int Width) RenderChain(List<RenderedSegment> chain, Row row)
    {
        var (sepGlyph, solid) = Seps[row.Separator];
        var sb = new StringBuilder();
        int width = 0;

        void Emit(string text, string? fg, string? bg, StyledRun? attrs = null)
        {
            if (text.Length == 0) return;
            sb.Append(Ansi.Reset);
            sb.Append(Ansi.Sgr(fg, bg,
                attrs?.Bold ?? false, attrs?.Dim ?? false, attrs?.Italic ?? false, attrs?.Underline ?? false));
            sb.Append(text);
            width += Fmt.Width(text);
        }

        if (solid)
        {
            if (row.Caps != CapStyle.None)
                Emit(Caps[row.Caps].Left, chain[0].Bg, null);

            for (int idx = 0; idx < chain.Count; idx++)
            {
                var seg = chain[idx];
                Emit(" ", seg.Fg, seg.Bg);
                EmitRuns(seg);
                Emit(" ", seg.Fg, seg.Bg);

                if (idx < chain.Count - 1)
                    Emit(sepGlyph, seg.Bg, chain[idx + 1].Bg);
                else if (row.Caps != CapStyle.None)
                    Emit(Caps[row.Caps].Right, seg.Bg, null);
            }
        }
        else
        {
            for (int idx = 0; idx < chain.Count; idx++)
            {
                var seg = chain[idx];
                if (Ansi.IsSet(seg.Bg)) Emit(" ", seg.Fg, seg.Bg);
                EmitRuns(seg);
                if (Ansi.IsSet(seg.Bg)) Emit(" ", seg.Fg, seg.Bg);

                if (idx < chain.Count - 1)
                {
                    if (sepGlyph.Length == 0) Emit(" ", null, null);
                    else if (sepGlyph == " ") Emit(" ", null, null);
                    else Emit(" " + sepGlyph + " ", row.SeparatorFg, null);
                }
            }
        }

        return (sb.ToString(), width);

        void EmitRuns(RenderedSegment seg)
        {
            foreach (var run in seg.Runs)
                Emit(run.Text, run.Fg ?? seg.Fg, run.Bg ?? seg.Bg, run);
        }
    }
}
