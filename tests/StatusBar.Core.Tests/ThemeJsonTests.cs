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
/// Theme-level JSON contract for the working palette: first-class data that round-trips,
/// and stays out of the JSON entirely when absent (keeps share URLs lean, and old
/// renderers ignore it either way).
/// </summary>
public class ThemeJsonTests
{
    [Fact]
    public void Palette_round_trips_through_theme_json()
    {
        var theme = new Theme { Palette = new ObservableCollection<string> { "#9ece6a", "#1a1b26" } };
        var reloaded = Theme.FromJson(theme.ToJson());
        Assert.Equal(new[] { "#9ece6a", "#1a1b26" }, reloaded.Palette);
    }

    [Fact]
    public void Absent_palette_keeps_json_lean()
    {
        var json = new Theme().ToJson();
        Assert.DoesNotContain("palette", json, StringComparison.OrdinalIgnoreCase);
        Assert.Null(Theme.FromJson(json).Palette);
    }

    [Fact]
    public void Section_caps_and_match_fg_round_trip_and_stay_lean_by_default()
    {
        var theme = new Theme();
        theme.Rows.Add(new Row
        {
            Segments = { new Segment { Element = ElementKind.ContextPercent, Bar = new BarOptions() } },
        });
        var lean = theme.ToJson();
        Assert.DoesNotContain("sectionCaps", lean);
        Assert.DoesNotContain("matchFg", lean);

        theme.Rows[0].Segments[0].SectionCaps = CapStyle.Slant;
        theme.Rows[0].Segments[0].Bar!.MatchFg = true;
        var reloaded = Theme.FromJson(theme.ToJson());
        Assert.Equal(CapStyle.Slant, reloaded.Rows[0].Segments[0].SectionCaps);
        Assert.True(reloaded.Rows[0].Segments[0].Bar!.MatchFg);
    }
}
