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

/// <summary>Row composition pins that don't fit the width or bar-zone suites.</summary>
public class ComposerTests
{
    [Fact]
    public void Blank_row_line_survives_whitespace_trimming()
    {
        // Claude Code's display drops empty and whitespace-only statusline rows (space and
        // NBSP both verified dead 2026-07-21, tools/test-blankline.js). Deliberate blank
        // rows must therefore emit braille blank U+2800: not whitespace, renders empty.
        var line = Composer.ComposeRow(new Row(), new SampleData().Build());
        Assert.Contains('\u2800', line);
        Assert.False(string.IsNullOrWhiteSpace(line));
    }

    // --- Section caps: Segment.SectionCaps overrides Row.Caps for the spacer-split
    // capsule it sits in. Pinned by equivalence with row-level caps, so the tests
    // never need the NF cap glyphs spelled out.

    static string Compose(Row row) => Composer.ComposeRow(row, new SampleData().Build());

    static Segment Chip(string text, CapStyle? caps = null) =>
        new() { Element = ElementKind.Text, Text = text, Bg = "#333333", Fg = "#eeeeee", SectionCaps = caps };

    static Row RowOf(CapStyle caps, params Segment[] segs)
    {
        var row = new Row { Caps = caps };
        foreach (var s in segs) row.Segments.Add(s);
        return row;
    }

    [Fact]
    public void Section_caps_override_matches_row_level_equivalent()
    {
        Assert.Equal(
            Compose(RowOf(CapStyle.Round, Chip("hi"))),
            Compose(RowOf(CapStyle.Chevron, Chip("hi", CapStyle.Round))));
    }

    [Fact]
    public void Explicit_none_override_strips_caps()
    {
        Assert.Equal(
            Compose(RowOf(CapStyle.None, Chip("hi"))),
            Compose(RowOf(CapStyle.Round, Chip("hi", CapStyle.None))));
    }

    [Fact]
    public void First_set_override_in_the_section_wins()
    {
        Assert.Equal(
            Compose(RowOf(CapStyle.Round, Chip("a"), Chip("b"))),
            Compose(RowOf(CapStyle.Chevron, Chip("a"), Chip("b", CapStyle.Round))));
    }

    [Fact]
    public void Sections_carry_their_own_caps()
    {
        Row Mixed(CapStyle rowCaps, CapStyle? firstSection) => RowOf(rowCaps,
            Chip("a", firstSection),
            new Segment { Element = ElementKind.Spacer, Text = "2" },
            Chip("b"));
        var mixed = Compose(Mixed(CapStyle.Chevron, CapStyle.Round));
        Assert.NotEqual(Compose(Mixed(CapStyle.Chevron, null)), mixed); // first section restyled
        Assert.NotEqual(Compose(Mixed(CapStyle.Round, null)), mixed);   // second section untouched
    }

    // --- Section separator: Segment.SectionSeparator overrides Row.Separator with the
    // same first-set-wins rule, and the section's whole solid/thin layout follows it.
    // Pinned by equivalence with the row-level separator, like the caps tests above.

    static Segment SepChip(string text, SeparatorStyle? sep = null) =>
        new() { Element = ElementKind.Text, Text = text, Bg = "#333333", Fg = "#eeeeee", SectionSeparator = sep };

    static Row SepRow(SeparatorStyle sep, params Segment[] segs)
    {
        var row = new Row { Separator = sep };
        foreach (var s in segs) row.Segments.Add(s);
        return row;
    }

    [Fact]
    public void Section_separator_override_matches_row_level_equivalent()
    {
        // Thin override on a solid row: the whole section drops to the thin layout.
        Assert.Equal(
            Compose(SepRow(SeparatorStyle.Dot, SepChip("a"), SepChip("b"))),
            Compose(SepRow(SeparatorStyle.Chevron, SepChip("a", SeparatorStyle.Dot), SepChip("b"))));
    }

    [Fact]
    public void Thin_row_section_can_go_solid()
    {
        Assert.Equal(
            Compose(SepRow(SeparatorStyle.Round, SepChip("a"), SepChip("b"))),
            Compose(SepRow(SeparatorStyle.Pipe, SepChip("a", SeparatorStyle.Round), SepChip("b"))));
    }

    [Fact]
    public void Sections_carry_their_own_separator()
    {
        Row Mixed(SeparatorStyle rowSep, SeparatorStyle? firstSection) => SepRow(rowSep,
            SepChip("a", firstSection), SepChip("a2"),
            new Segment { Element = ElementKind.Spacer, Text = "2" },
            SepChip("b"), SepChip("b2"));
        var mixed = Compose(Mixed(SeparatorStyle.Chevron, SeparatorStyle.Dot));
        Assert.NotEqual(Compose(Mixed(SeparatorStyle.Chevron, null)), mixed); // first section restyled
        Assert.NotEqual(Compose(Mixed(SeparatorStyle.Dot, null)), mixed);     // second section untouched
    }
}
