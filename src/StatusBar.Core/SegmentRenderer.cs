// Claude Status Bar Builder — a visual designer for Claude Code status lines.
// Copyright (C) 2026 Stephen Shanafelt
// SPDX-License-Identifier: GPL-3.0-only
//
// This program is free software: you can redistribute it and/or modify it under
// the terms of the GNU General Public License as published by the Free Software
// Foundation, version 3. It is distributed WITHOUT ANY WARRANTY; see the full
// license text in the LICENSE file at the root of this repository.

namespace StatusBar.Core;

/// <summary>A rendered segment: its runs plus the effective (threshold-adjusted) segment colors.</summary>
public sealed class RenderedSegment
{
    public required List<StyledRun> Runs { get; init; }
    public string? Fg { get; init; }
    public string? Bg { get; init; }
}

public static class SegmentRenderer
{
    const string GreenDefault = "#98c379";
    const string RedDefault = "#e06c75";
    const string YellowDefault = "#e5c07b";
    const string GrayDefault = "#8a919e";

    /// <summary>Renders one segment, or returns null when its data is missing and it hides.</summary>
    public static RenderedSegment? Render(Segment seg, RenderContext ctx)
    {
        var def = ElementRegistry.Get(seg.Element);
        var (value, percent, customRuns) = Evaluate(seg, ctx);

        bool missing = value is null && customRuns is null && percent is null;
        if (missing && seg.HideWhenMissing) return null;

        // Threshold override (percent-valued elements only)
        string? fg = seg.Fg, bg = seg.Bg;
        if (percent is double p && seg.Thresholds.Count > 0)
        {
            ThresholdRule? best = null;
            foreach (var rule in seg.Thresholds)
                if (p >= rule.AtOrAbove && (best is null || rule.AtOrAbove >= best.AtOrAbove))
                    best = rule;
            if (best is not null)
            {
                if (Ansi.IsSet(best.Fg)) fg = best.Fg;
                if (Ansi.IsSet(best.Bg)) bg = best.Bg;
            }
        }

        string icon = seg.Icon ?? def.DefaultIcon;
        string label = seg.Label ?? def.DefaultLabel;
        bool hideValueText = seg.Format == "hidden";

        var runs = new List<StyledRun>();
        void Add(string text, string? runFg = null) => runs.Add(new StyledRun(text, runFg));
        void Space() { if (runs.Count > 0) Add(" "); }

        if (icon.Length > 0) { Add(icon, seg.IconFg); }
        if (label.Length > 0) { Space(); Add(label, seg.LabelFg); }
        if (seg.Bar is BarOptions bar && percent is double pv) { Space(); AddBar(runs, bar, pv, seg, fg); }
        if (!hideValueText)
        {
            if (customRuns is not null) { Space(); runs.AddRange(customRuns); }
            else if (!string.IsNullOrEmpty(value)) { Space(); Add(value, seg.ValueFg); }
        }

        if (runs.Count == 0) return null;

        // Segment-level text attributes apply to every run.
        if (seg.Bold || seg.Dim || seg.Italic || seg.Underline)
            for (int r = 0; r < runs.Count; r++)
                runs[r] = runs[r] with
                {
                    Bold = runs[r].Bold || seg.Bold,
                    Dim = runs[r].Dim || seg.Dim,
                    Italic = runs[r].Italic || seg.Italic,
                    Underline = runs[r].Underline || seg.Underline,
                };

        return new RenderedSegment { Runs = runs, Fg = fg, Bg = bg };
    }

    /// <summary>Produces the segment's value text, percent (for bars/thresholds), and custom-colored runs.</summary>
    static (string? Value, double? Percent, List<StyledRun>? CustomRuns) Evaluate(Segment seg, RenderContext ctx)
    {
        var i = ctx.Input;
        var c = ctx.Computed;
        var cw = i.ContextWindow;

        switch (seg.Element)
        {
            case ElementKind.Model:
                return (seg.Format == "id" ? i.Model?.Id : i.Model?.DisplayName, null, null);
            case ElementKind.Effort:
                return (i.Effort?.Level, null, null);
            case ElementKind.Thinking:
                return (i.Thinking?.Enabled == true ? "thinking" : null, null, null);
            case ElementKind.OutputStyle:
                return (i.OutputStyle?.Name, null, null);
            case ElementKind.AgentName:
                return (i.Agent?.Name, null, null);
            case ElementKind.SessionName:
                return (i.SessionName, null, null);
            case ElementKind.VimMode:
                return (i.Vim?.Mode, null, null);
            case ElementKind.CcVersion:
                return (i.Version, null, null);

            case ElementKind.Directory:
                return (NullIfEmpty(Fmt.Dir(i.Workspace?.CurrentDir ?? i.Cwd, seg.Format)), null, null);
            case ElementKind.RepoName:
            {
                var repo = i.Workspace?.Repo;
                if (repo?.Name is null) return (null, null, null);
                return (seg.Format == "ownerName" && repo.Owner is not null ? repo.Owner + "/" + repo.Name : repo.Name, null, null);
            }
            case ElementKind.WorktreeName:
                return (i.Workspace?.GitWorktree ?? i.Worktree?.Name, null, null);
            case ElementKind.PrStatus:
            {
                if (i.Pr?.Number is not long n) return (null, null, null);
                string v = "#" + n;
                if (seg.Format == "numberState" && i.Pr.ReviewState is string st) v += " " + st;
                return (v, null, null);
            }

            case ElementKind.ContextPercent:
            {
                double? pct = ContextUsedPercent(cw);
                return (pct is double p ? Fmt.Percent(p, seg.Format) : null, pct, null);
            }
            case ElementKind.ContextRemaining:
            {
                double? used = ContextUsedPercent(cw);
                double? pct = cw?.RemainingPercentage ?? (used is double u ? 100 - u : null);
                return (pct is double p ? Fmt.Percent(p, seg.Format) : null, pct, null);
            }
            case ElementKind.ContextTokens:
            {
                if (cw?.TotalInputTokens is not long t) return (null, null, null);
                string v = Tok(t, seg.Format);
                if (seg.Option == "withMax" && cw.ContextWindowSize is long size) v += "/" + Fmt.Compact(size);
                return (v, null, null);
            }
            case ElementKind.TokensIn:
                return (TokOrNull(cw?.CurrentUsage?.InputTokens, seg.Format), null, null);
            case ElementKind.TokensOut:
                return (TokOrNull(cw?.CurrentUsage?.OutputTokens ?? cw?.TotalOutputTokens, seg.Format), null, null);
            case ElementKind.TokensCached:
                return (TokOrNull(cw?.CurrentUsage?.CacheReadInputTokens, seg.Format), null, null);
            case ElementKind.TokensTotal:
            {
                if (cw?.TotalInputTokens is not long ti) return (null, null, null);
                return (Tok(ti + (cw.TotalOutputTokens ?? 0), seg.Format), null, null);
            }

            case ElementKind.Cost:
                return (i.Cost?.TotalCostUsd is double usd ? Fmt.Usd(usd, seg.Format == "4dp" ? 4 : 2) : null, null, null);
            case ElementKind.Duration:
                return (i.Cost?.TotalDurationMs is double ms ? Fmt.DurationMs(ms, seg.Format) : null, null, null);
            case ElementKind.ApiDuration:
                return (i.Cost?.TotalApiDurationMs is double ams ? Fmt.DurationMs(ams, seg.Format) : null, null, null);
            case ElementKind.LinesChanged:
                return PlusMinus(i.Cost?.TotalLinesAdded, i.Cost?.TotalLinesRemoved, seg);
            case ElementKind.BurnRate:
            {
                if (i.Cost?.TotalCostUsd is not double cost || i.Cost.TotalDurationMs is not double dur || dur < 60_000)
                    return (null, null, null);
                double perHour = cost / (dur / 3_600_000.0);
                return (seg.Format == "day"
                    ? Fmt.Usd(perHour * 24, 2) + "/day"
                    : Fmt.Usd(perHour, 2) + "/hr", null, null);
            }

            case ElementKind.Limit5h:
                return RateWindow(i.RateLimits?.FiveHour, seg, ctx, weekday: false);
            case ElementKind.Limit7d:
                return RateWindow(i.RateLimits?.SevenDay, seg, ctx, weekday: true);

            case ElementKind.GitBranch:
                return (c.GitBranch, null, null);
            case ElementKind.GitStatus:
            {
                int total = c.GitStaged + c.GitModified + c.GitUntracked;
                if (c.GitBranch is null || total == 0) return (null, null, null);
                if (seg.Format == "detail")
                {
                    var runs = new List<StyledRun>();
                    if (c.GitStaged > 0) runs.Add(new("+" + c.GitStaged, seg.ValueFg ?? GreenDefault));
                    if (c.GitModified > 0) { if (runs.Count > 0) runs.Add(new(" ")); runs.Add(new("~" + c.GitModified, seg.ValueFg ?? YellowDefault)); }
                    if (c.GitUntracked > 0) { if (runs.Count > 0) runs.Add(new(" ")); runs.Add(new("?" + c.GitUntracked, seg.ValueFg ?? GrayDefault)); }
                    return (null, null, runs);
                }
                return ("● " + total, null, null);
            }
            case ElementKind.GitAheadBehind:
            {
                if (c.GitBranch is null || (c.GitAhead == 0 && c.GitBehind == 0)) return (null, null, null);
                string v = (c.GitAhead > 0 ? "↑" + c.GitAhead : "") + (c.GitBehind > 0 ? "↓" + c.GitBehind : "");
                return (v, null, null);
            }
            case ElementKind.GitDiff:
                return c.GitBranch is null ? (null, null, null) : PlusMinus(c.GitLinesAdded, c.GitLinesRemoved, seg);

            case ElementKind.Clock:
                return (Fmt.Clock(c.Now, seg.Format), null, null);
            case ElementKind.DateToday:
                return (seg.Format == "iso" ? c.Now.ToString("yyyy-MM-dd") : c.Now.ToString("ddd MMM d").ToLowerInvariant(), null, null);
            case ElementKind.ProjectVersion:
                return (c.ProjectVersion, null, null);
            case ElementKind.Spinner:
            {
                var frames = SpinnerFrames.GetValueOrDefault(seg.Format ?? "") ?? SpinnerFrames["braille"];
                long idx = c.Now.ToUnixTimeMilliseconds() / 300 % frames.Length;
                return (frames[idx], null, null);
            }

            case ElementKind.Text:
                return (NullIfEmpty(seg.Text), null, null);

            default:
                return (null, null, null);
        }
    }

    static double? ContextUsedPercent(ContextWindowInfo? cw)
    {
        if (cw?.UsedPercentage is double p) return p;
        if (cw?.TotalInputTokens is long t && cw.ContextWindowSize is long size && size > 0)
            return t * 100.0 / size;
        return null;
    }

    static (string?, double?, List<StyledRun>?) RateWindow(RateWindowInfo? w, Segment seg, RenderContext ctx, bool weekday)
    {
        if (w?.UsedPercentage is not double pct) return (null, null, null);
        string v = Fmt.Percent(pct, seg.Format);
        if (w.ResetsAt is long resets)
        {
            string extra = seg.Option switch
            {
                "time" => "(" + Fmt.TimeAt(resets, weekday) + ")",
                "countdown" => Fmt.Countdown(resets, ctx.Computed.Now),
                "both" => Fmt.Countdown(resets, ctx.Computed.Now) + " (" + Fmt.TimeAt(resets, weekday) + ")",
                _ => "",
            };
            if (extra.Length > 0) v += " " + extra;
        }
        return (v, pct, null);
    }

    static (string?, double?, List<StyledRun>?) PlusMinus(long? added, long? removed, Segment seg)
    {
        if (added is not long a || removed is not long r) return (null, null, null);
        if (a == 0 && r == 0) return (null, null, null);
        if (Ansi.IsSet(seg.ValueFg)) return ("+" + a + ",-" + r, null, null);
        var runs = new List<StyledRun>
        {
            new("+" + a, GreenDefault),
            new(","),
            new("-" + r, RedDefault),
        };
        return (null, null, runs);
    }

    static string Tok(long n, string? fmt) => fmt == "full" ? n.ToString("N0") : Fmt.Compact(n);
    static string? TokOrNull(long? n, string? fmt) => n is long v ? Tok(v, fmt) : null;
    static string? NullIfEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;

    static readonly Dictionary<string, string[]> SpinnerFrames = new()
    {
        ["braille"] = new[] { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" },
        ["dots"] = new[] { "⣾", "⣽", "⣻", "⢿", "⡿", "⣟", "⣯", "⣷" },
        ["moon"] = new[] { "🌑", "🌒", "🌓", "🌔", "🌕", "🌖", "🌗", "🌘" },
        ["clock"] = new[] { "🕐", "🕑", "🕒", "🕓", "🕔", "🕕", "🕖", "🕗", "🕘", "🕙", "🕚", "🕛" },
        ["pulse"] = new[] { "▁", "▂", "▃", "▄", "▅", "▆", "▇", "█", "▇", "▆", "▅", "▄", "▃", "▂" },
        ["arrow"] = new[] { "←", "↖", "↑", "↗", "→", "↘", "↓", "↙" },
    };

    // Sub-character fill: index = eighths of a cell (1-7), full block handled separately.
    static readonly string[] Eighths = { "", "▏", "▎", "▍", "▌", "▋", "▊", "▉" };

    static void AddBar(List<StyledRun> runs, BarOptions bar, double percent, Segment seg, string? effFg)
    {
        int width = Math.Max(1, bar.Width);
        string? fill = bar.FilledFg ?? seg.ValueFg ?? effFg;
        string? gradFrom = Ansi.ToHex(bar.GradientFrom);
        string? gradTo = Ansi.ToHex(bar.GradientTo);
        bool gradient = gradFrom is not null && gradTo is not null;

        string? CellColor(int index) => gradient
            ? Ansi.Lerp(gradFrom!, gradTo!, width <= 1 ? 0 : (double)index / (width - 1))
            : fill;

        double cellsExact = Math.Clamp(percent, 0, 100) / 100.0 * width;
        int full = (int)Math.Floor(cellsExact);
        double frac = cellsExact - full;

        if (bar.Brackets is { Length: >= 2 })
            runs.Add(new StyledRun(bar.Brackets[0].ToString(), seg.ValueFg ?? effFg));

        int cell = 0;
        for (; cell < full; cell++)
            runs.Add(new StyledRun(bar.FilledChar, CellColor(cell)));

        if (cell < width && bar.Smooth && bar.FilledChar == "█")
        {
            int e = (int)Math.Round(frac * 8);
            if (e >= 8) { runs.Add(new StyledRun("█", CellColor(cell))); cell++; }
            else if (e > 0) { runs.Add(new StyledRun(Eighths[e], CellColor(cell))); cell++; }
        }

        for (; cell < width; cell++)
            runs.Add(new StyledRun(bar.EmptyChar, bar.EmptyFg));

        if (bar.Brackets is { Length: >= 2 })
            runs.Add(new StyledRun(bar.Brackets[1].ToString(), seg.ValueFg ?? effFg));
    }
}
