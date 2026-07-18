// Claude Status Bar Builder — a visual designer for Claude Code status lines.
// Copyright (C) 2026 Stephen Shanafelt
// SPDX-License-Identifier: GPL-3.0-only
//
// This program is free software: you can redistribute it and/or modify it under
// the terms of the GNU General Public License as published by the Free Software
// Foundation, version 3. It is distributed WITHOUT ANY WARRANTY; see the full
// license text in the LICENSE file at the root of this repository.

using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using StatusBar.Core;

namespace StatusBar.Builder;

public sealed class FormatChoice
{
    public string? Key { get; init; }
    public string Display { get; init; } = "";
}

public partial class MainWindow : Window, INotifyPropertyChanged
{
    public static SeparatorStyle[] SeparatorChoices { get; } = (SeparatorStyle[])Enum.GetValues(typeof(SeparatorStyle));
    public static CapStyle[] CapChoices { get; } = (CapStyle[])Enum.GetValues(typeof(CapStyle));

    static readonly string[] QuickIcons =
    {
        "✳", "🤖", "🧠", "📁", "📦", "📅", "🕐", "⏱", "⚡", "💰", "🌿", "🌱", "🔥", "⭐", "🦫",
        "📊", "🔗", "💡", "●", "◆", "▲", "✦", "➜",
        "", "", "", "", "", "", "", "",
    };

    static readonly string[] BarCharPresets = { "█ ░", "▓ ░", "■ □", "● ○", "▰ ▱", "⣿ ⣀", "▮ ▯", "★ ☆" };

    public event PropertyChangedEventHandler? PropertyChanged;

    Theme _theme = DefaultTheme.Create();
    public Theme CurrentTheme
    {
        get => _theme;
        set
        {
            _theme = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentTheme)));
            SelectSegment(null, null);
            HookAll();
            ScheduleRender();
        }
    }

    public SampleData Sample { get; } = new();

    readonly DispatcherTimer _renderTimer;
    readonly DispatcherTimer _spinTimer;
    readonly List<INotifyPropertyChanged> _hookedNpc = new();
    readonly List<INotifyCollectionChanged> _hookedCol = new();

    Segment? _selected;
    string? _themePath;
    bool _webReady;
    bool _suppressInspector;
    bool _suppressSelection;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        _renderTimer.Tick += (_, _) => { _renderTimer.Stop(); RenderNow(); };

        // Keeps the preview animating while a Spinner element is in the theme.
        _spinTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _spinTimer.Tick += (_, _) => RenderNow();

        Sample.PropertyChanged += (_, _) => ScheduleRender();

        BuildPalette();
        BuildIconQuickPanel();
        foreach (var preset in BarCharPresets) BarCharCombo.Items.Add(preset);

        HookAll();
        Loaded += async (_, _) => await InitWebViewAsync();
    }

    // ── Preview ─────────────────────────────────────────────────────────────

    async Task InitWebViewAsync()
    {
        try
        {
            var env = await CoreWebView2Environment.CreateAsync(null,
                Path.Combine(Path.GetTempPath(), "StatusBarBuilder.WebView2"));
            await Web.EnsureCoreWebView2Async(env);
            Web.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "preview.app", Path.Combine(AppContext.BaseDirectory, "Assets"),
                CoreWebView2HostResourceAccessKind.Allow);
            Web.CoreWebView2.NavigationCompleted += (_, _) => { _webReady = true; ScheduleRender(); };
            Web.CoreWebView2.Navigate("https://preview.app/preview.html");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "Could not start the preview terminal (WebView2 runtime missing?).\n\n" + ex.Message,
                "Preview unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    void ScheduleRender()
    {
        _renderTimer.Stop();
        _renderTimer.Start();
    }

    async void RenderNow()
    {
        if (!_webReady) return;
        try
        {
            var ctx = Sample.Build();
            string text = string.Join("\n", Composer.Compose(CurrentTheme, ctx));
            string bg = Ansi.ToHex(CurrentTheme.Background) ?? "#101014";
            string js = $"renderStatus({JsonSerializer.Serialize(text)},{JsonSerializer.Serialize(CurrentTheme.FontFamily)},{JsonSerializer.Serialize(bg)})";
            await Web.CoreWebView2.ExecuteScriptAsync(js);

            bool hasSpinner = CurrentTheme.Rows.Any(r => r.Segments.Any(s => s.Element == ElementKind.Spinner));
            if (hasSpinner && !_spinTimer.IsEnabled) _spinTimer.Start();
            else if (!hasSpinner && _spinTimer.IsEnabled) _spinTimer.Stop();
        }
        catch
        {
            // never let a render hiccup take the app down
        }
    }

    // ── Change tracking (anything in the theme → rerender) ──────────────────

    void HookAll()
    {
        foreach (var npc in _hookedNpc) npc.PropertyChanged -= OnThemePropChanged;
        foreach (var col in _hookedCol) col.CollectionChanged -= OnThemeCollectionChanged;
        _hookedNpc.Clear();
        _hookedCol.Clear();

        void HookNpc(INotifyPropertyChanged npc) { npc.PropertyChanged += OnThemePropChanged; _hookedNpc.Add(npc); }
        void HookCol(INotifyCollectionChanged col) { col.CollectionChanged += OnThemeCollectionChanged; _hookedCol.Add(col); }

        HookNpc(CurrentTheme);
        HookCol(CurrentTheme.Rows);
        foreach (var row in CurrentTheme.Rows)
        {
            HookNpc(row);
            HookCol(row.Segments);
            foreach (var seg in row.Segments)
            {
                HookNpc(seg);
                HookCol(seg.Thresholds);
                foreach (var t in seg.Thresholds) HookNpc(t);
                if (seg.Bar is not null) HookNpc(seg.Bar);
            }
        }
    }

    void OnThemePropChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Segment.Bar))
            Dispatcher.BeginInvoke(HookAll); // a BarOptions object was created/removed
        ScheduleRender();
    }

    void OnThemeCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(HookAll);
        ScheduleRender();
    }

    // ── Element palette ─────────────────────────────────────────────────────

    void BuildPalette()
    {
        foreach (var group in ElementRegistry.All.GroupBy(d => d.Group))
        {
            PaletteHost.Children.Add(new TextBlock
            {
                Text = group.Key,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Margin = new Thickness(0, 8, 0, 2),
            });
            var wrap = new WrapPanel();
            foreach (var def in group)
            {
                var btn = new Button
                {
                    Content = def.Name,
                    Tag = def.Kind,
                    FontSize = 11,
                    Margin = new Thickness(2),
                    Padding = new Thickness(6, 2, 6, 2),
                    ToolTip = def.Description,
                };
                btn.Click += AddElement_Click;
                wrap.Children.Add(btn);
            }
            PaletteHost.Children.Add(wrap);
        }
    }

    void AddElement_Click(object sender, RoutedEventArgs e)
    {
        var kind = (ElementKind)((Button)sender).Tag;
        var def = ElementRegistry.Get(kind);
        var seg = new Segment
        {
            Element = kind,
            Label = def.DefaultLabel,
            Icon = def.DefaultIcon,
            Format = def.Formats is { Length: > 0 } ? def.Formats[0].Key : null,
            Option = def.Options is { Length: > 0 } ? def.Options[0].Key : null,
        };
        if (kind == ElementKind.Text) seg.Text = "hello";
        if (kind == ElementKind.Spacer) seg.Text = "flex";
        if (kind == ElementKind.Limit5h || kind == ElementKind.Limit7d) seg.Option = "time";

        if (CurrentTheme.Rows.Count == 0) CurrentTheme.Rows.Add(new Row());
        Row targetRow = FindRowOf(_selected) ?? CurrentTheme.Rows[^1];
        int index = _selected is not null && targetRow.Segments.Contains(_selected)
            ? targetRow.Segments.IndexOf(_selected) + 1
            : targetRow.Segments.Count;
        targetRow.Segments.Insert(index, seg);

        // Select the new chip once its container exists.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            var lb = FindListBoxFor(targetRow);
            if (lb is not null) lb.SelectedItem = seg;
        });
    }

    internal static readonly FontFamily IconButtonFont = new(
        new Uri("file:///" + AppContext.BaseDirectory.Replace('\\', '/') + "Assets/fonts/"),
        "./#CaskaydiaCove Nerd Font Mono, ./#CaskaydiaCove NFM, Segoe UI Emoji, Segoe UI Symbol, Cascadia Code");

    void BuildIconQuickPanel()
    {
        var clear = new Button { Content = "∅", FontSize = 12, Width = 26, Height = 24, Margin = new Thickness(1), ToolTip = "No icon" };
        clear.Click += (_, _) => IconBox.Text = "";
        IconQuickPanel.Children.Add(clear);

        foreach (var glyph in QuickIcons)
        {
            var btn = new Button
            {
                Content = glyph,
                FontSize = 13,
                Width = 26,
                Height = 24,
                Margin = new Thickness(1),
                FontFamily = IconButtonFont,
            };
            if (glyph[0] >= '\uE000' && glyph[0] <= '\uF8FF')
                btn.ToolTip = "Nerd Font glyph — your terminal needs a Nerd Font to show it";
            btn.Click += (_, _) => IconBox.Text = glyph;
            IconQuickPanel.Children.Add(btn);
        }
    }

    // ── Selection & inspector ───────────────────────────────────────────────

    void SegmentList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelection) return;
        var lb = (ListBox)sender;
        if (lb.SelectedItem is not Segment seg) return;

        _suppressSelection = true;
        foreach (var other in FindVisualChildren<ListBox>(RowsHost))
            if (!ReferenceEquals(other, lb))
                other.SelectedItem = null;
        _suppressSelection = false;

        SelectSegment(seg, lb);
    }

    void SelectSegment(Segment? seg, ListBox? host)
    {
        _selected = seg;
        _suppressInspector = true;

        InspectorRoot.IsEnabled = seg is not null;
        InspectorRoot.DataContext = seg;

        if (seg is null)
        {
            ElementTitle.Text = "Nothing selected";
            ElementDesc.Text = "";
            _suppressInspector = false;
            return;
        }

        var def = ElementRegistry.Get(seg.Element);
        ElementTitle.Text = def.Name;
        ElementDesc.Text = def.Description;

        bool isLayout = seg.Element is ElementKind.Text or ElementKind.Spacer;
        TextPanel.Visibility = isLayout ? Visibility.Visible : Visibility.Collapsed;
        TextPanelLabel.Text = seg.Element == ElementKind.Spacer ? "Spacer width ('flex' or a number)" : "Text";
        TextValueBox.Text = seg.Text ?? "";
        StylePanel.Visibility = seg.Element == ElementKind.Spacer ? Visibility.Collapsed : Visibility.Visible;

        LabelBox.Text = seg.Label ?? "";
        IconBox.Text = seg.Icon ?? "";

        PopulateChoiceCombo(FormatCombo, FormatPanel, def.Formats, seg.Format);
        PopulateChoiceCombo(OptionCombo, OptionPanel, def.Options, seg.Option);

        BarToggle.Visibility = def.SupportsBar ? Visibility.Visible : Visibility.Collapsed;
        BarToggle.IsChecked = seg.Bar is not null;
        BarPanel.Visibility = def.SupportsBar && seg.Bar is not null ? Visibility.Visible : Visibility.Collapsed;

        ThresholdSection.Visibility = def.SupportsThresholds ? Visibility.Visible : Visibility.Collapsed;

        _suppressInspector = false;
    }

    static void PopulateChoiceCombo(ComboBox combo, StackPanel panel, (string Key, string Display)[]? choices, string? current)
    {
        combo.Items.Clear();
        if (choices is null || choices.Length == 0)
        {
            panel.Visibility = Visibility.Collapsed;
            return;
        }
        panel.Visibility = Visibility.Visible;
        foreach (var (key, display) in choices)
            combo.Items.Add(new FormatChoice { Key = key, Display = display });
        combo.SelectedIndex = Math.Max(0, Array.FindIndex(choices, c => c.Key == current));
    }

    void FormatCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressInspector || _selected is null) return;
        if (FormatCombo.SelectedItem is FormatChoice c) _selected.Format = c.Key;
    }

    void OptionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressInspector || _selected is null) return;
        if (OptionCombo.SelectedItem is FormatChoice c) _selected.Option = c.Key;
    }

    void LabelBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressInspector || _selected is null) return;
        _selected.Label = LabelBox.Text;
    }

    void IconBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressInspector || _selected is null) return;
        _selected.Icon = IconBox.Text;
    }

    void BrowseIcons_Click(object sender, RoutedEventArgs e)
    {
        string? glyph = IconBrowserWindow.Pick(this);
        if (glyph is not null) IconBox.Text = glyph;
    }

    void TextValueBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressInspector || _selected is null) return;
        _selected.Text = TextValueBox.Text;
    }

    void BarToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressInspector || _selected is null) return;
        if (BarToggle.IsChecked == true && _selected.Bar is null)
            _selected.Bar = new BarOptions();
        else if (BarToggle.IsChecked != true)
            _selected.Bar = null;
        BarPanel.Visibility = _selected.Bar is not null ? Visibility.Visible : Visibility.Collapsed;
    }

    void BarCharCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressInspector || _selected?.Bar is null) return;
        if (BarCharCombo.SelectedItem is not string preset) return;
        var parts = preset.Split(' ');
        if (parts.Length == 2)
        {
            _selected.Bar.FilledChar = parts[0];
            _selected.Bar.EmptyChar = parts[1];
        }
    }

    void AddThreshold_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        double next = _selected.Thresholds.Count == 0 ? 70 : Math.Min(95, _selected.Thresholds.Max(t => t.AtOrAbove) + 20);
        _selected.Thresholds.Add(new ThresholdRule { AtOrAbove = next, Fg = "#e0af68" });
    }

    void RemoveThreshold_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        if (((Button)sender).Tag is ThresholdRule rule) _selected.Thresholds.Remove(rule);
    }

    // ── Row / segment structure ops ─────────────────────────────────────────

    void AddRow_Click(object sender, RoutedEventArgs e) => CurrentTheme.Rows.Add(new Row());

    void DeleteRow_Click(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).Tag is not Row row) return;
        if (_selected is not null && row.Segments.Contains(_selected)) SelectSegment(null, null);
        CurrentTheme.Rows.Remove(row);
    }

    void MoveLeft_Click(object sender, RoutedEventArgs e) => MoveSelected(-1);
    void MoveRight_Click(object sender, RoutedEventArgs e) => MoveSelected(+1);

    void MoveSelected(int delta)
    {
        if (_selected is null) return;
        var row = FindRowOf(_selected);
        if (row is null) return;
        int i = row.Segments.IndexOf(_selected);
        int j = i + delta;
        if (j < 0 || j >= row.Segments.Count) return;
        row.Segments.Move(i, j);
        var seg = _selected;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            var lb = FindListBoxFor(row);
            if (lb is not null) lb.SelectedItem = seg;
        });
    }

    void MoveRowUp_Click(object sender, RoutedEventArgs e) => MoveSelectedToRow(-1);
    void MoveRowDown_Click(object sender, RoutedEventArgs e) => MoveSelectedToRow(+1);

    void MoveSelectedToRow(int delta)
    {
        if (_selected is null) return;
        var row = FindRowOf(_selected);
        if (row is null) return;
        int target = CurrentTheme.Rows.IndexOf(row) + delta;
        if (target < 0 || target >= CurrentTheme.Rows.Count) return;

        var seg = _selected;
        var targetRow = CurrentTheme.Rows[target];
        row.Segments.Remove(seg);
        targetRow.Segments.Add(seg);
        _selected = seg;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            var lb = FindListBoxFor(targetRow);
            if (lb is not null) lb.SelectedItem = seg;
        });
    }

    void DeleteSegment_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        var row = FindRowOf(_selected);
        row?.Segments.Remove(_selected);
        SelectSegment(null, null);
    }

    Row? FindRowOf(Segment? seg) =>
        seg is null ? null : CurrentTheme.Rows.FirstOrDefault(r => r.Segments.Contains(seg));

    ListBox? FindListBoxFor(Row row) =>
        FindVisualChildren<ListBox>(RowsHost).FirstOrDefault(lb => ReferenceEquals(lb.DataContext, row));

    static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var deeper in FindVisualChildren<T>(child)) yield return deeper;
        }
    }

    // ── Theme files ─────────────────────────────────────────────────────────

    static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "StatusBarBuilder.slnx")))
                dir = dir.Parent;
            return dir?.FullName ?? @"C:\ClaudeStatusBarBuilder";
        }
    }

    static string ThemesDir
    {
        get
        {
            string d = Path.Combine(RepoRoot, "themes");
            Directory.CreateDirectory(d);
            return d;
        }
    }

    void New_Click(object sender, RoutedEventArgs e)
    {
        var theme = new Theme();
        theme.Rows.Add(new Row());
        _themePath = null;
        CurrentTheme = theme;
    }

    void LoadDefault_Click(object sender, RoutedEventArgs e)
    {
        _themePath = null;
        CurrentTheme = DefaultTheme.Create();
    }

    void Open_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Theme JSON|*.json", InitialDirectory = ThemesDir };
        if (dlg.ShowDialog(this) != true) return;
        var theme = Theme.TryLoad(dlg.FileName);
        if (theme is null)
        {
            MessageBox.Show(this, "Could not read that theme file.", "Open failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        _themePath = dlg.FileName;
        CurrentTheme = theme;
    }

    void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_themePath is null) { SaveAs_Click(sender, e); return; }
        CurrentTheme.Save(_themePath);
    }

    void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Theme JSON|*.json",
            InitialDirectory = ThemesDir,
            FileName = MakeFileSafe(CurrentTheme.Name) + ".json",
        };
        if (dlg.ShowDialog(this) != true) return;
        _themePath = dlg.FileName;
        CurrentTheme.Save(_themePath);
    }

    static string MakeFileSafe(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '-');
        return string.IsNullOrWhiteSpace(name) ? "theme" : name;
    }

    // ── Publish & apply ─────────────────────────────────────────────────────

    static string RendererExePath => Path.Combine(RepoRoot, "bin", "renderer", "StatusBar.Renderer.exe");

    async void Publish_Click(object sender, RoutedEventArgs e) => await PublishRendererAsync(showSuccess: true);

    async Task<bool> PublishRendererAsync(bool showSuccess)
    {
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"publish \"{Path.Combine(RepoRoot, "src", "StatusBar.Renderer")}\" -c Release -o \"{Path.Combine(RepoRoot, "bin", "renderer")}\" --nologo",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi)!;
            string output = await proc.StandardOutput.ReadToEndAsync();
            string err = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            if (proc.ExitCode != 0)
            {
                MessageBox.Show(this, "dotnet publish failed:\n\n" + output + "\n" + err,
                    "Publish failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            if (showSuccess)
                MessageBox.Show(this, "Renderer published to:\n" + RendererExePath,
                    "Publish complete", MessageBoxButton.OK, MessageBoxImage.Information);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Publish failed: " + ex.Message, "Publish failed", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    async void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(RendererExePath))
        {
            if (MessageBox.Show(this,
                    "The renderer exe hasn't been published yet. Publish it now? (takes ~30s once)",
                    "Publish needed", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            if (!await PublishRendererAsync(showSuccess: false)) return;
        }

        try
        {
            string activeTheme = Path.Combine(ThemesDir, "active.json");
            CurrentTheme.Save(activeTheme);

            string settingsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
            Directory.CreateDirectory(settingsDir);
            string settingsPath = Path.Combine(settingsDir, "settings.json");

            JsonObject root;
            if (File.Exists(settingsPath))
            {
                File.Copy(settingsPath, settingsPath + ".bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), overwrite: false);
                root = JsonNode.Parse(File.ReadAllText(settingsPath)) as JsonObject ?? new JsonObject();
            }
            else
            {
                root = new JsonObject();
            }

            // Forward slashes: Claude Code runs this through Git Bash when installed,
            // and backslashes would be eaten as escapes there.
            string exe = RendererExePath.Replace('\\', '/');
            string themeArg = activeTheme.Replace('\\', '/');
            root["statusLine"] = new JsonObject
            {
                ["type"] = "command",
                ["command"] = $"\"{exe}\" \"{themeArg}\"",
            };

            File.WriteAllText(settingsPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            MessageBox.Show(this,
                "Done! statusLine now points at your theme.\n\n" +
                "Theme: " + activeTheme + "\n" +
                "Settings: " + settingsPath + " (previous version backed up)\n\n" +
                "It shows up on your next interaction in Claude Code.",
                "Applied", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not update settings: " + ex.Message,
                "Apply failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
