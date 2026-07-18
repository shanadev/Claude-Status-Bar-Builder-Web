// Claude Status Bar Builder — a visual designer for Claude Code status lines.
// Copyright (C) 2026 Stephen Shanafelt
// SPDX-License-Identifier: GPL-3.0-only
//
// This program is free software: you can redistribute it and/or modify it under
// the terms of the GNU General Public License as published by the Free Software
// Foundation, version 3. It is distributed WITHOUT ANY WARRANTY; see the full
// license text in the LICENSE file at the root of this repository.

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace StatusBar.Builder;

public sealed class IconEntry
{
    [JsonPropertyName("c")] public string C { get; set; } = "";
    [JsonPropertyName("n")] public string N { get; set; } = "";
    [JsonPropertyName("k")] public string K { get; set; } = "";
    [JsonPropertyName("g")] public string G { get; set; } = "";
}

/// <summary>
/// Searchable catalog of every emoji (with CLDR keywords, so "dino" finds 🦖)
/// and the full Nerd Font glyph set (Assets/icons.json, ~12,700 entries).
/// </summary>
public partial class IconBrowserWindow : Window
{
    const string AllIcons = "All icons";
    const string AllEmoji = "All emoji";
    const string AllNerdFont = "All Nerd Font";
    const int MaxResults = 400;

    static List<IconEntry>? _icons;      // loaded once per process
    static string[]? _haystacks;         // lowercase "name keywords group" per icon

    readonly DispatcherTimer _debounce;

    public string? SelectedGlyph { get; private set; }

    public IconBrowserWindow()
    {
        InitializeComponent();
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _debounce.Tick += (_, _) => { _debounce.Stop(); Refresh(); };
        Loaded += (_, _) =>
        {
            LoadIcons();
            BuildSetCombo();
            Refresh();
            SearchBox.Focus();
        };
    }

    /// <summary>Shows the browser; returns the chosen glyph or null if cancelled.</summary>
    public static string? Pick(Window? owner)
    {
        var dlg = new IconBrowserWindow { Owner = owner };
        return dlg.ShowDialog() == true ? dlg.SelectedGlyph : null;
    }

    static void LoadIcons()
    {
        if (_icons is not null) return;
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Assets", "icons.json");
            _icons = JsonSerializer.Deserialize<List<IconEntry>>(File.ReadAllText(path)) ?? new();
        }
        catch
        {
            _icons = new();
        }
        _haystacks = _icons.Select(i => (i.N + " " + i.K + " " + i.G).ToLowerInvariant()).ToArray();
    }

    void BuildSetCombo()
    {
        SetCombo.Items.Add(AllIcons);
        SetCombo.Items.Add(AllEmoji);
        SetCombo.Items.Add(AllNerdFont);
        foreach (var g in _icons!.Select(i => i.G).Distinct())
            SetCombo.Items.Add(g);
        SetCombo.SelectedIndex = 0;
    }

    void Search_Changed(object sender, TextChangedEventArgs e) { _debounce.Stop(); _debounce.Start(); }
    void Filter_Changed(object sender, SelectionChangedEventArgs e) => Refresh();

    void Refresh()
    {
        if (_icons is null || !IsLoaded) return;

        string[] terms = SearchBox.Text.Trim().ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string set = SetCombo.SelectedItem as string ?? AllIcons;

        ResultsPanel.Children.Clear();
        int shown = 0, matched = 0;
        for (int i = 0; i < _icons.Count; i++)
        {
            var icon = _icons[i];
            bool inSet = set switch
            {
                AllIcons => true,
                AllEmoji => icon.G.StartsWith("Emoji"),
                AllNerdFont => icon.G.StartsWith("NF"),
                _ => icon.G == set,
            };
            if (!inSet) continue;

            bool ok = true;
            foreach (var t in terms)
                if (!_haystacks![i].Contains(t)) { ok = false; break; }
            if (!ok) continue;

            matched++;
            if (shown < MaxResults)
            {
                ResultsPanel.Children.Add(MakeButton(icon));
                shown++;
            }
        }

        CountText.Text = matched > shown
            ? $"showing {shown} of {matched} matches — keep typing to narrow down"
            : $"{matched} match{(matched == 1 ? "" : "es")}";
    }

    Button MakeButton(IconEntry icon)
    {
        var btn = new Button
        {
            Content = icon.C,
            FontSize = 18,
            Width = 36,
            Height = 34,
            Margin = new Thickness(1),
            FontFamily = MainWindow.IconButtonFont,
            ToolTip = icon.N
                + (icon.K.Length > 0 ? "\n" + icon.K : "")
                + "\n" + icon.G,
        };
        btn.Click += (_, _) => { SelectedGlyph = icon.C; DialogResult = true; };
        return btn;
    }
}
