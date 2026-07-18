// Claude Status Bar Builder — a visual designer for Claude Code status lines.
// Copyright (C) 2026 Stephen Shanafelt
// SPDX-License-Identifier: GPL-3.0-only
//
// This program is free software: you can redistribute it and/or modify it under
// the terms of the GNU General Public License as published by the Free Software
// Foundation, version 3. It is distributed WITHOUT ANY WARRANTY; see the full
// license text in the LICENSE file at the root of this repository.

namespace StatusBar.Core;

/// <summary>Ships a good-looking starting point (Tokyo Night palette, powerline capsules).</summary>
public static class DefaultTheme
{
    public static Theme Create()
    {
        var theme = new Theme { Name = "Default" };

        var row = new Row { Separator = SeparatorStyle.Chevron, Caps = CapStyle.Round };
        row.Segments.Add(new Segment
        {
            Element = ElementKind.Model,
            Icon = "✳", Label = "",
            Bg = "#7aa2f7", Fg = "#16161e", Bold = true,
        });
        row.Segments.Add(new Segment
        {
            Element = ElementKind.ContextTokens,
            Icon = "", Label = "Ctx:",
            Bg = "#3b4261", Fg = "#c0caf5",
        });
        row.Segments.Add(new Segment
        {
            Element = ElementKind.ContextPercent,
            Icon = "", Label = "",
            Bg = "#24283b", Fg = "#a9b1d6",
            Bar = new BarOptions
            {
                Width = 10, Smooth = true,
                GradientFrom = "#9ece6a", GradientTo = "#f7768e",
                EmptyFg = "#3b4261",
            },
            Thresholds =
            {
                new ThresholdRule { AtOrAbove = 70, Fg = "#e0af68" },
                new ThresholdRule { AtOrAbove = 90, Fg = "#f7768e" },
            },
        });
        row.Segments.Add(new Segment
        {
            Element = ElementKind.GitBranch,
            Icon = "🌿", Label = "",
            Bg = "#9ece6a", Fg = "#16161e",
        });
        row.Segments.Add(new Segment
        {
            Element = ElementKind.GitDiff,
            Icon = "", Label = "",
            Bg = "#414868", Fg = "#c0caf5",
        });
        row.Segments.Add(new Segment
        {
            Element = ElementKind.Cost,
            Icon = "💰", Label = "",
            Bg = "#bb9af7", Fg = "#16161e",
        });
        row.Segments.Add(new Segment
        {
            Element = ElementKind.Limit5h,
            Icon = "", Label = "5h",
            Bg = "#7dcfff", Fg = "#16161e",
            Option = "time",
        });
        row.Segments.Add(new Segment
        {
            Element = ElementKind.Limit7d,
            Icon = "", Label = "wk",
            Bg = "#565f89", Fg = "#c0caf5",
            Option = "time",
        });
        row.Segments.Add(new Segment
        {
            Element = ElementKind.Clock,
            Icon = "", Label = "",
            Bg = "#24283b", Fg = "#a9b1d6",
        });
        theme.Rows.Add(row);

        return theme;
    }
}
