// Hack the Claude Status Bar — shared builder state (theme, sample data, selection).
// Copyright (C) 2026 Stephen Shanafelt
// SPDX-License-Identifier: GPL-3.0-only
//
// This program is free software: you can redistribute it and/or modify it under
// the terms of the GNU General Public License as published by the Free Software
// Foundation, version 3. It is distributed WITHOUT ANY WARRANTY; see the full
// license text in the LICENSE file at the root of this repository.

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using StatusBar.Core;

namespace StatusBar.Web.Services;

/// <summary>
/// The one instance of "what is being built": theme + sample data + selection.
/// Components mutate the theme, then call Notify() — the preview and panels re-render off Changed.
/// Autosaves to localStorage (debounced); loads from a #t= share link or the autosave on startup.
/// </summary>
public sealed class BuilderState : IDisposable
{
    const string AutosaveKey = "sbb-theme";

    readonly IJSRuntime _js;
    readonly Timer _saveTimer;
    bool _initialized;

    public BuilderState(IJSRuntime js)
    {
        _js = js;
        _saveTimer = new Timer(_ => _ = SaveNowAsync(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public Theme Theme { get; private set; } = DefaultTheme.Create();
    public SampleData Sample { get; } = new();
    public Row? SelectedRow { get; private set; }
    public Segment? Selected { get; private set; }
    /// <summary>Row that palette clicks append to (last selected row, defaults to the first).</summary>
    public Row TargetRow => SelectedRow ?? Theme.Rows[0];
    public bool RemixedFromLink { get; private set; }

    /// <summary>An element chip is mid-drag: a new kind from the palette, or an existing segment.</summary>
    public ElementKind? DragNewKind { get; set; }
    public (Row Row, Segment Seg)? DragExisting { get; set; }

    public event Action? Changed;

    public void Notify()
    {
        Changed?.Invoke();
        _saveTimer.Change(800, Timeout.Infinite);
    }

    /// <summary>Load order: #t= share link wins, else the localStorage autosave, else the default theme.</summary>
    public async Task InitializeAsync(NavigationManager nav)
    {
        if (_initialized) return;
        _initialized = true;

        var frag = new Uri(nav.Uri).Fragment;
        var marker = frag.IndexOf("t=", StringComparison.Ordinal);
        if (marker >= 0 && ThemeCodec.TryDecode(frag[(marker + 2)..]) is Theme shared)
        {
            Theme = shared;
            RemixedFromLink = true;
            Changed?.Invoke();
            return;
        }

        var saved = await _js.InvokeAsync<string?>("sbGet", AutosaveKey);
        if (!string.IsNullOrEmpty(saved))
        {
            try { Theme = Theme.FromJson(saved); Changed?.Invoke(); }
            catch { /* corrupt autosave — keep the default */ }
        }
    }

    async Task SaveNowAsync()
    {
        try { await _js.InvokeVoidAsync("sbSet", AutosaveKey, Theme.ToJson()); }
        catch { /* page tearing down */ }
    }

    public void Select(Row row, Segment seg) { SelectedRow = row; Selected = seg; Changed?.Invoke(); }
    public void SelectRow(Row row) { SelectedRow = row; Selected = null; Changed?.Invoke(); }
    public void ClearSelection() { Selected = null; Changed?.Invoke(); }

    public void ReplaceTheme(Theme theme, bool remixed = false)
    {
        Theme = theme;
        SelectedRow = null;
        Selected = null;
        RemixedFromLink = remixed;
        Notify();
    }

    // Colors cycled onto newly added segments so the chain looks intentional immediately
    // (Tokyo Night, same family as DefaultTheme).
    static readonly string[] NewSegmentBgs =
    {
        "#7aa2f7", "#3b4261", "#9ece6a", "#24283b", "#bb9af7", "#414868", "#7dcfff", "#565f89",
    };
    int _nextBg;

    public Segment AddElement(ElementKind kind, Row? row = null, int? index = null)
    {
        row ??= TargetRow;
        var seg = new Segment { Element = kind };
        if (kind == ElementKind.Spacer)
        {
            seg.Text = "flex";
        }
        else if (kind == ElementKind.Text)
        {
            seg.Text = "hack the planet";
            AssignColors(seg);
        }
        else
        {
            AssignColors(seg);
        }

        if (index is int i && i >= 0 && i <= row.Segments.Count) row.Segments.Insert(i, seg);
        else row.Segments.Add(seg);

        SelectedRow = row;
        Selected = seg;
        Notify();
        return seg;
    }

    void AssignColors(Segment seg)
    {
        var bg = NewSegmentBgs[_nextBg++ % NewSegmentBgs.Length];
        seg.Bg = bg;
        seg.Fg = Luma(bg) > 0.45 ? "#16161e" : "#c0caf5";
    }

    static double Luma(string hex) =>
        Ansi.TryParseHex(hex, out byte r, out byte g, out byte b)
            ? (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255.0 : 0;

    public void RemoveSegment(Row row, Segment seg)
    {
        row.Segments.Remove(seg);
        if (Selected == seg) Selected = null;
        Notify();
    }

    public void MoveSegment(Row fromRow, Segment seg, Row toRow, int toIndex)
    {
        int from = fromRow.Segments.IndexOf(seg);
        if (from < 0) return;
        fromRow.Segments.RemoveAt(from);
        if (fromRow == toRow && from < toIndex) toIndex--;
        toIndex = Math.Clamp(toIndex, 0, toRow.Segments.Count);
        toRow.Segments.Insert(toIndex, seg);
        SelectedRow = toRow;
        Selected = seg;
        Notify();
    }

    public Row AddRow()
    {
        var row = new Row
        {
            Separator = Theme.Rows.LastOrDefault()?.Separator ?? SeparatorStyle.Chevron,
            Caps = Theme.Rows.LastOrDefault()?.Caps ?? CapStyle.Round,
        };
        Theme.Rows.Add(row);
        SelectedRow = row;
        Selected = null;
        Notify();
        return row;
    }

    public void RemoveRow(Row row)
    {
        if (Theme.Rows.Count <= 1) return;
        Theme.Rows.Remove(row);
        if (SelectedRow == row) { SelectedRow = null; Selected = null; }
        Notify();
    }

    /// <summary>Build a render context with the terminal's real column count.</summary>
    public RenderContext BuildContext(int columns)
    {
        var ctx = Sample.Build();
        ctx.Computed.Columns = columns;
        return ctx;
    }

    public void Dispose() => _saveTimer.Dispose();
}
