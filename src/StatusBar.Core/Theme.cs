// Claude Status Bar Builder — a visual designer for Claude Code status lines.
// Copyright (C) 2026 Stephen Shanafelt
// SPDX-License-Identifier: GPL-3.0-only
//
// This program is free software: you can redistribute it and/or modify it under
// the terms of the GNU General Public License as published by the Free Software
// Foundation, version 3. It is distributed WITHOUT ANY WARRANTY; see the full
// license text in the LICENSE file at the root of this repository.

using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StatusBar.Core;

/// <summary>Separator drawn between segments in a row. "Solid" styles get powerline color chaining.</summary>
public enum SeparatorStyle { None, Space, Pipe, Dot, ChevronLine, Chevron, Round, Slant, Backslant, Flame, Blocks }

/// <summary>End caps drawn at both ends of a run of solid segments.</summary>
public enum CapStyle { None, Chevron, Round, Slant, Backslant, Flame, Blocks }

public partial class Theme : ObservableObject
{
    [ObservableProperty] private string _name = "My Status Line";
    [ObservableProperty] private string _fontFamily = "CaskaydiaCove NFM";
    [ObservableProperty] private string _background = "#101014";
    public ObservableCollection<Row> Rows { get; set; } = new();

    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOpts);
    public static Theme FromJson(string json) => JsonSerializer.Deserialize<Theme>(json, JsonOpts) ?? new Theme();

    public static Theme? TryLoad(string path)
    {
        try { return File.Exists(path) ? FromJson(File.ReadAllText(path)) : null; }
        catch { return null; }
    }

    public void Save(string path) => File.WriteAllText(path, ToJson());
}

public partial class Row : ObservableObject
{
    [ObservableProperty] private SeparatorStyle _separator = SeparatorStyle.Chevron;
    [ObservableProperty] private CapStyle _caps = CapStyle.Round;
    [ObservableProperty] private string? _separatorFg; // used by the thin separators (Pipe, Dot, ChevronLine)
    public ObservableCollection<Segment> Segments { get; set; } = new();
}

public partial class Segment : ObservableObject
{
    [ObservableProperty] private ElementKind _element;
    [ObservableProperty] private string? _label;   // null = element default, "" = hidden
    [ObservableProperty] private string? _icon;    // null = element default, "" = hidden
    [ObservableProperty] private string? _text;    // Text element content; Spacer width ("flex" or a number)
    [ObservableProperty] private string? _fg;
    [ObservableProperty] private string? _bg;
    [ObservableProperty] private string? _iconFg;
    [ObservableProperty] private string? _labelFg;
    [ObservableProperty] private string? _valueFg;
    [ObservableProperty] private bool _bold;
    [ObservableProperty] private bool _dim;
    [ObservableProperty] private bool _italic;
    [ObservableProperty] private bool _underline;
    [ObservableProperty] private string? _format;  // element-specific primary format key
    [ObservableProperty] private string? _option;  // element-specific secondary option key
    [ObservableProperty] private bool _hideWhenMissing = true;
    [ObservableProperty] private BarOptions? _bar;
    public ObservableCollection<ThresholdRule> Thresholds { get; set; } = new();
}

public partial class BarOptions : ObservableObject
{
    [ObservableProperty] private int _width = 10;
    [ObservableProperty] private string _filledChar = "█";
    [ObservableProperty] private string _emptyChar = "░";
    [ObservableProperty] private bool _smooth = true; // 1/8-block sub-character resolution (█ fill only)
    [ObservableProperty] private string? _filledFg;
    [ObservableProperty] private string? _emptyFg = "#3b4261";
    [ObservableProperty] private string? _gradientFrom;
    [ObservableProperty] private string? _gradientTo;
    [ObservableProperty] private string? _brackets;   // two chars, e.g. "[]"
}

/// <summary>When the segment's percent value is >= AtOrAbove, these colors override the segment's.</summary>
public partial class ThresholdRule : ObservableObject
{
    [ObservableProperty] private double _atOrAbove;
    [ObservableProperty] private string? _fg;
    [ObservableProperty] private string? _bg;
}
