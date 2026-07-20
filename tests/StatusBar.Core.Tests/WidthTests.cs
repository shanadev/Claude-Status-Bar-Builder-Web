// Claude Status Bar Builder - a visual designer for Claude Code status lines.
// Copyright (C) 2026 Stephen Shanafelt
// SPDX-License-Identifier: GPL-3.0-only
//
// This program is free software: you can redistribute it and/or modify it under
// the terms of the GNU General Public License as published by the Free Software
// Foundation, version 3. It is distributed WITHOUT ANY WARRANTY; see the full
// license text in the LICENSE file at the root of this repository.

using StatusBar.Core;
using Xunit;

namespace StatusBar.Core.Tests;

/// <summary>
/// Fmt.Width must agree with the preview terminal Unicode 11 provider cell-for-cell
/// (see UnicodeWidth.cs provenance) - these pins cover the glyph classes a status
/// line actually uses. Expected values were read from the xterm unicode11 addon.
/// All glyphs are escape-sequence-only per repo convention (no invisible chars).
/// </summary>
public class WidthTests
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("abc", 3)]
    [InlineData("\u23F0", 2)]                     // alarm-clock emoji below U+1F000 - the acid burn regression
    [InlineData("\U0001F33F", 2)]                 // herb emoji (git branch icon)
    [InlineData("\u26A1", 2)]                     // high voltage
    [InlineData("\u2B50", 2)]                     // star
    [InlineData("\uE0B0", 1)]                     // Nerd Font powerline chevron - PUA stays narrow
    [InlineData("\u2588", 1)]                     // full block (progress bars)
    [InlineData("\u28FF", 1)]                     // braille (spinner frames)
    [InlineData("\u4E00", 2)]                     // CJK ideograph
    [InlineData("e\u0301", 1)]                    // combining acute collapses onto its base
    [InlineData("\u2764", 1)]                     // text-presentation heart stays narrow (xterm parity)
    [InlineData("\u2764\uFE0F", 1)]               // VS16 is zero-width in the preview tables
    [InlineData("\U0001F468\u200D\U0001F469", 4)] // ZWJ pair renders as two glyphs: 2+0+2
    [InlineData("\u23F0 4:40pm", 9)]              // icon + built-in space + text
    public void Width_matches_preview_terminal(string s, int expected) =>
        Assert.Equal(expected, Fmt.Width(s));

    [Theory]
    [InlineData(0x07, 0)]     // C0 controls are zero-width in the provider table
    [InlineData(0x41, 1)]     // capital A via the ASCII fast path
    [InlineData(0x23F0, 2)]
    [InlineData(0xE0B0, 1)]
    [InlineData(0x10FFFD, 1)] // plane-16 PUA, above every table entry
    public void Of_returns_cell_count(int codepoint, int expected) =>
        Assert.Equal(expected, UnicodeWidth.Of(codepoint));
}
