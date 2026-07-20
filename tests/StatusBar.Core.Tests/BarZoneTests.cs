// Claude Status Bar Builder - a visual designer for Claude Code status lines.
// Copyright (C) 2026 Stephen Shanafelt
// SPDX-License-Identifier: GPL-3.0-only
//
// This program is free software: you can redistribute it and/or modify it under
// the terms of the GNU General Public License as published by the Free Software
// Foundation, version 3. It is distributed WITHOUT ANY WARRANTY; see the full
// license text in the LICENSE file at the root of this repository.

using System.Collections.ObjectModel;
using Xunit;

namespace StatusBar.Core.Tests;

/// <summary>
/// Positional bar color zones (BarOptions.Stops) plus the compatibility guarantee:
/// a theme using only GradientFrom/To must render exactly as it did before zones existed.
/// </summary>
public class BarZoneTests
{
    static List<StyledRun> BarRuns(BarOptions bar, double percent)
    {
        var ctx = new SampleData { ContextPercent = percent }.Build();
        var seg = new Segment
        {
            Element = ElementKind.ContextPercent,
            Icon = "", Label = "", Format = "hidden",
            Bar = bar,
        };
        var rendered = SegmentRenderer.Render(seg, ctx);
        Assert.NotNull(rendered);
        return rendered!.Runs;
    }

    static ObservableCollection<BarStop> Stops(params (double UpTo, string From)[] stops) =>
        new(stops.Select(s => new BarStop { UpTo = s.UpTo, From = s.From }));

    [Fact]
    public void Legacy_gradient_renders_exact_pre_zone_colors()
    {
        // t = index/(width-1), the v1.1.x formula - pinned so the zones refactor can never drift it.
        var runs = BarRuns(new BarOptions { Width = 4, GradientFrom = "#000000", GradientTo = "#0000ff" }, 100);
        Assert.Equal(new[] { "#000000", "#000055", "#0000aa", "#0000ff" }, runs.Select(r => r.Fg));
    }

    [Fact]
    public void Single_full_stop_equals_legacy_gradient()
    {
        var legacy = BarRuns(new BarOptions { Width = 10, GradientFrom = "#00ff66", GradientTo = "#ffb300" }, 100);
        var zoned = BarRuns(new BarOptions
        {
            Width = 10,
            Stops = new() { new BarStop { UpTo = 100, From = "#00ff66", To = "#ffb300" } },
        }, 100);
        Assert.Equal(legacy.Select(r => (r.Text, r.Fg)), zoned.Select(r => (r.Text, r.Fg)));
    }

    [Fact]
    public void Zones_pick_by_cell_center_percent()
    {
        var bar = new BarOptions { Width = 10, Stops = Stops((60, "#111111"), (85, "#222222"), (100, "#333333")) };
        var fgs = BarRuns(bar, 100).Select(r => r.Fg);
        // cell centers 5..55 fall in zone 1, 65/75/85 in zone 2, 95 in zone 3
        Assert.Equal(
            Enumerable.Repeat("#111111", 6).Concat(Enumerable.Repeat("#222222", 3)).Append("#333333"),
            fgs);
    }

    [Fact]
    public void Stop_order_does_not_matter()
    {
        var sorted = BarRuns(new BarOptions { Width = 10, Stops = Stops((60, "#111111"), (100, "#333333")) }, 100);
        var reversed = BarRuns(new BarOptions { Width = 10, Stops = Stops((100, "#333333"), (60, "#111111")) }, 100);
        Assert.Equal(sorted.Select(r => r.Fg), reversed.Select(r => r.Fg));
    }

    [Fact]
    public void Zone_mini_gradient_spans_its_own_cells()
    {
        var bar = new BarOptions
        {
            Width = 10,
            Stops = new()
            {
                new BarStop { UpTo = 50, From = "#000000", To = "#0000ff" },
                new BarStop { UpTo = 100, From = "#ffffff" },
            },
        };
        var fgs = BarRuns(bar, 100).Select(r => r.Fg).ToList();
        Assert.Equal(new[] { "#000000", "#000040", "#000080", "#0000bf", "#0000ff" }, fgs.Take(5));
        Assert.All(fgs.Skip(5), fg => Assert.Equal("#ffffff", fg));
    }

    [Fact]
    public void Tinted_track_dims_zone_colors_past_the_fill()
    {
        var bar = new BarOptions
        {
            Width = 10,
            TintTrack = true,
            Stops = Stops((60, "#9ece6a"), (85, "#e0af68"), (100, "#f7768e")),
        };
        var runs = BarRuns(bar, 50);
        Assert.All(runs.Take(5), r => { Assert.Equal("\u2588", r.Text); Assert.Equal("#9ece6a", r.Fg); });
        Assert.All(runs.Skip(5), r => Assert.Equal("\u2591", r.Text));
        Assert.Equal(Ansi.Lerp("#9ece6a", "#000000", 0.65), runs[5].Fg); // center 55%: still the green zone
        Assert.Equal(Ansi.Lerp("#e0af68", "#000000", 0.65), runs[6].Fg);
        Assert.Equal(Ansi.Lerp("#f7768e", "#000000", 0.65), runs[9].Fg);
    }

    [Fact]
    public void Untinted_track_keeps_empty_fg()
    {
        var runs = BarRuns(new BarOptions { Width = 10, Stops = Stops((100, "#9ece6a")) }, 50);
        Assert.All(runs.Skip(5), r => Assert.Equal("#3b4261", r.Fg));
    }

    [Fact]
    public void Stops_round_trip_through_theme_json()
    {
        var theme = new Theme();
        theme.Rows.Add(new Row
        {
            Segments =
            {
                new Segment
                {
                    Element = ElementKind.ContextPercent,
                    Bar = new BarOptions
                    {
                        TintTrack = true,
                        Stops = new()
                        {
                            new BarStop { UpTo = 60, From = "#9ece6a" },
                            new BarStop { UpTo = 100, From = "#e0af68", To = "#f7768e" },
                        },
                    },
                },
            },
        });

        var bar = Theme.FromJson(theme.ToJson()).Rows[0].Segments[0].Bar!;
        Assert.True(bar.TintTrack);
        Assert.Collection(bar.Stops!,
            s => { Assert.Equal(60, s.UpTo); Assert.Equal("#9ece6a", s.From); Assert.Null(s.To); },
            s => { Assert.Equal(100, s.UpTo); Assert.Equal("#e0af68", s.From); Assert.Equal("#f7768e", s.To); });
    }

    [Fact]
    public void Legacy_bar_serializes_without_zone_fields_and_reloads()
    {
        var theme = new Theme();
        theme.Rows.Add(new Row
        {
            Segments =
            {
                new Segment
                {
                    Element = ElementKind.ContextPercent,
                    Bar = new BarOptions { GradientFrom = "#00ff66", GradientTo = "#ffb300" },
                },
            },
        });

        var json = theme.ToJson();
        Assert.DoesNotContain("stops", json);     // null, not [] - keeps share URLs lean
        Assert.DoesNotContain("tintTrack", json); // default-off, like iconSpace

        var bar = Theme.FromJson(json).Rows[0].Segments[0].Bar!;
        Assert.Null(bar.Stops);
        Assert.False(bar.TintTrack);
        Assert.Equal("#00ff66", bar.GradientFrom);
    }
}
