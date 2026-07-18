// Claude Status Bar Builder — a visual designer for Claude Code status lines.
// Copyright (C) 2026 Stephen Shanafelt
// SPDX-License-Identifier: GPL-3.0-only
//
// This program is free software: you can redistribute it and/or modify it under
// the terms of the GNU General Public License as published by the Free Software
// Foundation, version 3. It is distributed WITHOUT ANY WARRANTY; see the full
// license text in the LICENSE file at the root of this repository.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using StatusBar.Core;

namespace StatusBar.Builder;

/// <summary>Compact color editor: label, swatch, spec textbox, and a palette popup.</summary>
public partial class ColorField : UserControl
{
    public static readonly DependencyProperty SpecProperty = DependencyProperty.Register(
        nameof(Spec), typeof(string), typeof(ColorField),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSpecChanged));

    public static readonly DependencyProperty LabelTextProperty = DependencyProperty.Register(
        nameof(LabelText), typeof(string), typeof(ColorField),
        new PropertyMetadata("", OnLabelChanged));

    static readonly string[] ExtraPalette =
    {
        "#16161e", "#1a1b26", "#24283b", "#292e42", "#3b4261", "#414868", "#565f89",
        "#7aa2f7", "#7dcfff", "#2ac3de", "#73daca", "#9ece6a", "#e0af68", "#ff9e64",
        "#f7768e", "#bb9af7", "#c0caf5", "#a9b1d6", "#ffffff", "#000000",
    };

    bool _updating;

    public string? Spec
    {
        get => (string?)GetValue(SpecProperty);
        set => SetValue(SpecProperty, value);
    }

    public string LabelText
    {
        get => (string)GetValue(LabelTextProperty);
        set => SetValue(LabelTextProperty, value);
    }

    public ColorField()
    {
        InitializeComponent();

        for (int i = 0; i < Ansi.Ansi16.Length; i++)
            AddSwatch("ansi:" + i, Ansi.Ansi16[i].Hex, $"ansi:{i} ({Ansi.Ansi16[i].Name})");
        foreach (var hex in ExtraPalette)
            AddSwatch(hex, hex, hex);
    }

    void AddSwatch(string spec, string hex, string tooltip)
    {
        var btn = new Button
        {
            Width = 22,
            Height = 22,
            Margin = new Thickness(1),
            Tag = spec,
            ToolTip = tooltip,
            Background = BrushFromHex(hex),
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
        };
        btn.Click += (s, e) =>
        {
            Spec = (string)((Button)s!).Tag;
            PickBtn.IsChecked = false;
        };
        SwatchPanel.Children.Add(btn);
    }

    static Brush BrushFromHex(string? hex)
    {
        if (Ansi.TryParseHex(hex, out byte r, out byte g, out byte b))
            return new SolidColorBrush(Color.FromRgb(r, g, b));
        return Brushes.Transparent;
    }

    static void OnSpecChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (ColorField)d;
        string text = (string?)e.NewValue ?? "";
        if (!self._updating && self.SpecBox.Text != text)
        {
            self._updating = true;
            self.SpecBox.Text = text;
            self._updating = false;
        }
        self.Swatch.Background = BrushFromHex(Ansi.ToHex(text));
    }

    static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ColorField)d).LabelBlock.Text = (string?)e.NewValue ?? "";
    }

    void SpecBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updating) return;
        _updating = true;
        Spec = SpecBox.Text.Length == 0 ? null : SpecBox.Text;
        _updating = false;
    }

    void None_Click(object sender, RoutedEventArgs e)
    {
        Spec = null;
        PickBtn.IsChecked = false;
    }

    void Custom_Click(object sender, RoutedEventArgs e)
    {
        PickBtn.IsChecked = false;
        string? hex = ColorPickerDialog.Pick(Window.GetWindow(this), Ansi.ToHex(Spec));
        if (hex is not null) Spec = hex;
    }
}
