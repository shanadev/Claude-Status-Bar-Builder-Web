// Claude Status Bar Builder — a visual designer for Claude Code status lines.
// Copyright (C) 2026 Stephen Shanafelt
// SPDX-License-Identifier: GPL-3.0-only
//
// This program is free software: you can redistribute it and/or modify it under
// the terms of the GNU General Public License as published by the Free Software
// Foundation, version 3. It is distributed WITHOUT ANY WARRANTY; see the full
// license text in the LICENSE file at the root of this repository.

namespace StatusBar.Core;

public enum ElementKind
{
    // Session
    Model, Effort, Thinking, OutputStyle, AgentName, SessionName, VimMode, CcVersion,
    // Workspace
    Directory, RepoName, WorktreeName, PrStatus,
    // Context window
    ContextPercent, ContextRemaining, ContextTokens, TokensIn, TokensOut, TokensCached, TokensTotal,
    // Cost & time
    Cost, Duration, ApiDuration, LinesChanged, BurnRate,
    // Rate limits
    Limit5h, Limit7d,
    // Git (computed)
    GitBranch, GitStatus, GitAheadBehind, GitDiff,
    // Extras (computed)
    Clock, DateToday, ProjectVersion, Spinner,
    // Layout
    Text, Spacer,
}

public sealed record ElementDef(
    ElementKind Kind,
    string Name,
    string Group,
    string DefaultIcon,
    string DefaultLabel,
    bool SupportsBar,
    bool SupportsThresholds,
    (string Key, string Display)[]? Formats,
    (string Key, string Display)[]? Options,
    string Description);

public static class ElementRegistry
{
    static readonly (string, string)[] PercentFormats = { ("0dp", "42%"), ("1dp", "41.8%"), ("hidden", "no text (bar only)") };
    static readonly (string, string)[] TokenFormats = { ("compact", "18.6k"), ("full", "18,600") };
    static readonly (string, string)[] ResetOptions = { ("none", "no reset info"), ("time", "(2pm)"), ("countdown", "3hr 45m"), ("both", "3hr 45m (2pm)") };

    public static readonly IReadOnlyList<ElementDef> All = new List<ElementDef>
    {
        // ── Session ──────────────────────────────────────────────────────────
        new(ElementKind.Model, "Model", "Session", "✳", "",
            false, false, new[]{("name","Opus"),("id","claude-opus-4-8")}, null,
            "Current model (display name or full id)."),
        new(ElementKind.Effort, "Effort level", "Session", "", "",
            false, false, null, null,
            "Reasoning effort: low / medium / high / xhigh / max. Absent for models without effort."),
        new(ElementKind.Thinking, "Thinking", "Session", "🧠", "",
            false, false, null, null,
            "Shows when extended thinking is enabled (hides when off)."),
        new(ElementKind.OutputStyle, "Output style", "Session", "", "style",
            false, false, null, null,
            "Name of the active output style."),
        new(ElementKind.AgentName, "Agent", "Session", "🤖", "",
            false, false, null, null,
            "Agent name when running with --agent."),
        new(ElementKind.SessionName, "Session name", "Session", "", "",
            false, false, null, null,
            "Custom session name set with --name or /rename."),
        new(ElementKind.VimMode, "Vim mode", "Session", "", "",
            false, false, null, null,
            "NORMAL / INSERT / VISUAL when vim mode is on."),
        new(ElementKind.CcVersion, "CC version", "Session", "", "CC",
            false, false, null, null,
            "Claude Code version."),

        // ── Workspace ────────────────────────────────────────────────────────
        new(ElementKind.Directory, "Directory", "Workspace", "📁", "",
            false, false, new[]{("name","my-app"),("short","C:/…/my-app"),("full","C:/dev/my-app")}, null,
            "Current working directory."),
        new(ElementKind.RepoName, "Repository", "Workspace", "", "",
            false, false, new[]{("name","my-app"),("ownerName","steve/my-app")}, null,
            "Repo identity from the origin remote."),
        new(ElementKind.WorktreeName, "Worktree", "Workspace", "🌱", "",
            false, false, null, null,
            "Git worktree name when inside a linked worktree."),
        new(ElementKind.PrStatus, "Pull request", "Workspace", "", "PR",
            false, false, new[]{("number","#123"),("numberState","#123 pending")}, null,
            "Open PR for the current branch."),

        // ── Context window ───────────────────────────────────────────────────
        new(ElementKind.ContextPercent, "Context used %", "Context", "", "Ctx",
            true, true, PercentFormats, null,
            "Percent of the context window used. The classic progress-bar element."),
        new(ElementKind.ContextRemaining, "Context left %", "Context", "", "Left",
            true, true, PercentFormats, null,
            "Percent of the context window remaining."),
        new(ElementKind.ContextTokens, "Context tokens", "Context", "", "Ctx",
            false, false, TokenFormats, new[]{("plain","18.6k"),("withMax","18.6k/200k")},
            "Tokens currently in the context window."),
        new(ElementKind.TokensIn, "Input tokens", "Context", "", "In",
            false, false, TokenFormats, null,
            "Fresh input tokens from the last API call."),
        new(ElementKind.TokensOut, "Output tokens", "Context", "", "Out",
            false, false, TokenFormats, null,
            "Output tokens from the last API call."),
        new(ElementKind.TokensCached, "Cached tokens", "Context", "", "Cached",
            false, false, TokenFormats, null,
            "Cache-read tokens from the last API call."),
        new(ElementKind.TokensTotal, "Total tokens", "Context", "", "Total",
            false, false, TokenFormats, null,
            "Context input + output tokens combined."),

        // ── Cost & time ──────────────────────────────────────────────────────
        new(ElementKind.Cost, "Session cost", "Cost & Time", "💰", "",
            false, false, new[]{("2dp","$0.45"),("4dp","$0.4512")}, null,
            "Estimated session cost in USD (resets on /clear)."),
        new(ElementKind.Duration, "Session time", "Cost & Time", "⏱", "",
            false, false, new[]{("hrmin","3hr 45m"),("colons","3:45:12"),("minsec","225m 12s")}, null,
            "Wall-clock time since the session started."),
        new(ElementKind.ApiDuration, "API time", "Cost & Time", "⚡", "",
            false, false, new[]{("hrmin","1hr 2m"),("colons","1:02:12"),("minsec","62m 12s")}, null,
            "Time spent waiting on API responses."),
        new(ElementKind.LinesChanged, "Lines +/-", "Cost & Time", "", "",
            false, false, null, null,
            "Session lines added/removed, e.g. +42,-10 (green/red)."),
        new(ElementKind.BurnRate, "Burn rate", "Cost & Time", "🔥", "",
            false, false, new[]{("hr","$0.45/hr"),("day","$10.80/day")}, null,
            "Session cost extrapolated to $/hour or $/day. Hides for the first minute (too noisy)."),

        // ── Rate limits ──────────────────────────────────────────────────────
        new(ElementKind.Limit5h, "5-hour limit", "Limits", "", "5h",
            true, true, PercentFormats, ResetOptions,
            "5-hour rolling rate limit used % (Pro/Max plans)."),
        new(ElementKind.Limit7d, "Weekly limit", "Limits", "", "wk",
            true, true, PercentFormats, ResetOptions,
            "7-day rate limit used %. Reset shows weekday, e.g. (fri,3am)."),

        // ── Git ──────────────────────────────────────────────────────────────
        new(ElementKind.GitBranch, "Git branch", "Git", "🌿", "",
            false, false, null, null,
            "Current branch name."),
        new(ElementKind.GitStatus, "Git dirty", "Git", "", "",
            false, false, new[]{("dots","● 6"),("detail","+2 ~3 ?1")}, null,
            "Working-tree status: staged / modified / untracked counts."),
        new(ElementKind.GitAheadBehind, "Ahead/behind", "Git", "", "",
            false, false, null, null,
            "Commits ahead/behind upstream, e.g. ↑1↓2. Hides when in sync."),
        new(ElementKind.GitDiff, "Git diff +/-", "Git", "", "",
            false, false, null, null,
            "Uncommitted line changes from git diff, e.g. +42,-10."),

        // ── Extras ───────────────────────────────────────────────────────────
        new(ElementKind.Clock, "Clock", "Extras", "🕐", "",
            false, false, new[]{("12h","3:45pm"),("24h","15:45"),("12h+sec","3:45:12pm")}, null,
            "Current local time (set a refresh interval to keep it ticking)."),
        new(ElementKind.DateToday, "Date", "Extras", "📅", "",
            false, false, new[]{("dddMMMd","fri jul 17"),("iso","2026-07-17")}, null,
            "Today's date."),
        new(ElementKind.ProjectVersion, "Project version", "Extras", "📦", "",
            false, false, null, null,
            "Version from package.json / *.csproj / pyproject.toml in the project dir."),
        new(ElementKind.Spinner, "Spinner", "Extras", "", "",
            false, false,
            new[]{("braille","⠧ braille"),("dots","⣟ dots"),("moon","🌒 moon"),("clock","🕑 clock"),("pulse","▅ pulse"),("arrow","↗ arrow")}, null,
            "Animated frames keyed to the clock. The status line only redraws while Claude is working (~every 300ms), so it spins during activity and freezes when idle."),

        // ── Layout ───────────────────────────────────────────────────────────
        new(ElementKind.Text, "Text", "Layout", "", "",
            false, false, null, null,
            "A literal piece of text (put anything you like here)."),
        new(ElementKind.Spacer, "Spacer / gap", "Layout", "", "",
            false, false, null, null,
            "Breaks the capsule chain. Width: a number of spaces, or 'flex' to push the rest right."),
    };

    static readonly Dictionary<ElementKind, ElementDef> ByKind = All.ToDictionary(d => d.Kind);
    public static ElementDef Get(ElementKind kind) => ByKind[kind];
}
