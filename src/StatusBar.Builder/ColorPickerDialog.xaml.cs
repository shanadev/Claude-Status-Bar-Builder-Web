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
using System.Windows.Input;
using System.Windows.Media;
using StatusBar.Core;

namespace StatusBar.Builder;

/// <summary>Full-RGB (truecolor) picker: hue strip + saturation/value square + hex box.</summary>
public partial class ColorPickerDialog : Window
{
    double _h = 220, _s = 0.6, _v = 0.9;
    bool _updatingHex;

    public string? SelectedHex { get; private set; }

    public ColorPickerDialog(string? initialHex)
    {
        InitializeComponent();
        if (Ansi.TryParseHex(initialHex, out byte r, out byte g, out byte b))
        {
            (_h, _s, _v) = RgbToHsv(Color.FromRgb(r, g, b));
            PreviewOld.Background = new SolidColorBrush(Color.FromRgb(r, g, b));
        }
        Loaded += (_, _) => SyncUi();
    }

    /// <summary>Shows the dialog; returns "#rrggbb" or null if cancelled.</summary>
    public static string? Pick(Window? owner, string? initialHex)
    {
        var dlg = new ColorPickerDialog(initialHex) { Owner = owner };
        return dlg.ShowDialog() == true ? dlg.SelectedHex : null;
    }

    void SyncUi()
    {
        SvBase.Fill = new SolidColorBrush(HsvToRgb(_h, 1, 1));
        var c = HsvToRgb(_h, _s, _v);
        PreviewNew.Background = new SolidColorBrush(c);
        if (!_updatingHex)
        {
            _updatingHex = true;
            HexBox.Text = $"#{c.R:x2}{c.G:x2}{c.B:x2}";
            _updatingHex = false;
        }
        Canvas.SetLeft(SvThumb, _s * SvBox.ActualWidth - SvThumb.Width / 2);
        Canvas.SetTop(SvThumb, (1 - _v) * SvBox.ActualHeight - SvThumb.Height / 2);
        Canvas.SetLeft(HueThumb, 0);
        Canvas.SetTop(HueThumb, _h / 360 * HueBox.ActualHeight - HueThumb.Height / 2);
    }

    void UpdateSv(Point p)
    {
        _s = Math.Clamp(p.X / SvBox.ActualWidth, 0, 1);
        _v = 1 - Math.Clamp(p.Y / SvBox.ActualHeight, 0, 1);
        SyncUi();
    }

    void UpdateHue(Point p)
    {
        _h = Math.Clamp(p.Y / HueBox.ActualHeight, 0, 1) * 360;
        SyncUi();
    }

    void SvBox_MouseDown(object sender, MouseButtonEventArgs e) { SvBox.CaptureMouse(); UpdateSv(e.GetPosition(SvBox)); }
    void SvBox_MouseMove(object sender, MouseEventArgs e) { if (SvBox.IsMouseCaptured) UpdateSv(e.GetPosition(SvBox)); }
    void SvBox_MouseUp(object sender, MouseButtonEventArgs e) => SvBox.ReleaseMouseCapture();

    void HueBox_MouseDown(object sender, MouseButtonEventArgs e) { HueBox.CaptureMouse(); UpdateHue(e.GetPosition(HueBox)); }
    void HueBox_MouseMove(object sender, MouseEventArgs e) { if (HueBox.IsMouseCaptured) UpdateHue(e.GetPosition(HueBox)); }
    void HueBox_MouseUp(object sender, MouseButtonEventArgs e) => HueBox.ReleaseMouseCapture();

    void HexBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingHex) return;
        if (!Ansi.TryParseHex(HexBox.Text, out byte r, out byte g, out byte b)) return;
        _updatingHex = true;
        (_h, _s, _v) = RgbToHsv(Color.FromRgb(r, g, b));
        SyncUi();
        _updatingHex = false;
    }

    void Ok_Click(object sender, RoutedEventArgs e)
    {
        var c = HsvToRgb(_h, _s, _v);
        SelectedHex = $"#{c.R:x2}{c.G:x2}{c.B:x2}";
        DialogResult = true;
    }

    static Color HsvToRgb(double h, double s, double v)
    {
        h = ((h % 360) + 360) % 360;
        double c = v * s;
        double x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        double m = v - c;
        (double r, double g, double b) = (int)(h / 60) switch
        {
            0 => (c, x, 0.0),
            1 => (x, c, 0.0),
            2 => (0.0, c, x),
            3 => (0.0, x, c),
            4 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };
        return Color.FromRgb(
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255));
    }

    static (double H, double S, double V) RgbToHsv(Color c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        double d = max - min;
        double h = 0;
        if (d > 0)
        {
            if (max == r) h = 60 * ((g - b) / d % 6);
            else if (max == g) h = 60 * ((b - r) / d + 2);
            else h = 60 * ((r - g) / d + 4);
            if (h < 0) h += 360;
        }
        return (h, max == 0 ? 0 : d / max, max);
    }
}
