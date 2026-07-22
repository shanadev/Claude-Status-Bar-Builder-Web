// Claude Status Bar Builder - a visual designer for Claude Code status lines.
// Copyright (C) 2026 Stephen Shanafelt
// SPDX-License-Identifier: GPL-3.0-only
//
// This program is free software: you can redistribute it and/or modify it under
// the terms of the GNU General Public License as published by the Free Software
// Foundation, version 3. It is distributed WITHOUT ANY WARRANTY; see the full
// license text in the LICENSE file at the root of this repository.

using Xunit;

namespace StatusBar.Core.Tests;

/// <summary>
/// BarOptions.MatchFg: segment text takes the fill tip's color (the last painted fill
/// cell; at 0%, the color the first cell would have), tracking threshold/zone/gradient
/// recolors. Bar cells themselves keep their own per-cell colors.
/// </summary>
public class MatchFgTests
{
    static RenderedSegment Render(double percent, bool match)
    {
        var ctx = new SampleData { ContextPercent = percent }.Build();
        var seg = new Segment
        {
            Element = ElementKind.ContextPercent,
            Icon = "", Label = "ctx", LabelFg = "#abcdef",
            Bar = new BarOptions
            {
                Width = 10,
                MatchFg = match,
                Stops = new()
                {
                    new BarStop { UpTo = 60, From = "#111111" },
                    new BarStop { UpTo = 100, From = "#333333" },
                },
            },
        };
        var rendered = SegmentRenderer.Render(seg, ctx);
        Assert.NotNull(rendered);
        return rendered!;
    }

    static StyledRun Label(RenderedSegment r) => r.Runs.First(x => x.Text == "ctx");

    [Fact]
    public void Text_follows_fill_tip_through_zones()
    {
        Assert.Equal("#111111", Label(Render(50, true)).Fg);  // tip = cell 4, low zone
        Assert.Equal("#333333", Label(Render(95, true)).Fg);  // smooth partial cell 9 is the tip
        Assert.Equal("#111111", Render(50, true).Fg);         // segment-level fg follows too
    }

    [Fact]
    public void Zero_percent_uses_the_first_cells_color()
    {
        Assert.Equal("#111111", Label(Render(0, true)).Fg);
    }

    [Fact]
    public void Off_leaves_configured_text_colors_alone()
    {
        Assert.Equal("#abcdef", Label(Render(95, false)).Fg);
    }

    [Fact]
    public void Bar_cells_keep_their_own_colors()
    {
        var runs = Render(95, true).Runs;
        Assert.Contains(runs, r => r.Fg == "#111111" && r.Text != "ctx"); // low-zone cells intact
    }
}
