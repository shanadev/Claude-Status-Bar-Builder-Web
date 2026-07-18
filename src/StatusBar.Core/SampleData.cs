// Claude Status Bar Builder — a visual designer for Claude Code status lines.
// Copyright (C) 2026 Stephen Shanafelt
// SPDX-License-Identifier: GPL-3.0-only
//
// This program is free software: you can redistribute it and/or modify it under
// the terms of the GNU General Public License as published by the Free Software
// Foundation, version 3. It is distributed WITHOUT ANY WARRANTY; see the full
// license text in the LICENSE file at the root of this repository.

using CommunityToolkit.Mvvm.ComponentModel;

namespace StatusBar.Core;

/// <summary>
/// Adjustable fake session data driving the Builder's live preview.
/// Build() shapes it into the same StatusInput/ComputedData the real renderer receives.
/// </summary>
public partial class SampleData : ObservableObject
{
    [ObservableProperty] private string _modelName = "Opus";
    [ObservableProperty] private string _modelId = "claude-opus-4-8";
    [ObservableProperty] private string _effortLevel = "high";
    [ObservableProperty] private bool _thinkingEnabled = true;
    [ObservableProperty] private string _outputStyle = "default";
    [ObservableProperty] private string _agentName = "";
    [ObservableProperty] private string _sessionName = "";
    [ObservableProperty] private string _vimMode = "";
    [ObservableProperty] private string _ccVersion = "2.1.90";

    [ObservableProperty] private string _cwd = "C:/dev/my-app";
    [ObservableProperty] private string _repoOwner = "steve";
    [ObservableProperty] private string _repoName = "my-app";
    [ObservableProperty] private string _worktreeName = "";
    [ObservableProperty] private bool _hasPr = false;
    [ObservableProperty] private double _prNumber = 123;
    [ObservableProperty] private string _prState = "pending";

    [ObservableProperty] private double _contextPercent = 42;
    [ObservableProperty] private double _contextSizeK = 200;
    [ObservableProperty] private double _tokensInK = 8.5;
    [ObservableProperty] private double _tokensOutK = 1.2;
    [ObservableProperty] private double _tokensCachedK = 12;

    [ObservableProperty] private double _costUsd = 0.45;
    [ObservableProperty] private double _durationMin = 60;
    [ObservableProperty] private double _apiMin = 14;
    [ObservableProperty] private double _linesAdded = 42;
    [ObservableProperty] private double _linesRemoved = 10;

    [ObservableProperty] private bool _hasLimits = true;
    [ObservableProperty] private double _limit5h = 78;
    [ObservableProperty] private double _limit7d = 37;
    [ObservableProperty] private double _limit5hResetHours = 2.5;
    [ObservableProperty] private double _limit7dResetHours = 60;

    [ObservableProperty] private string _gitBranch = "main";
    [ObservableProperty] private double _gitStaged = 2;
    [ObservableProperty] private double _gitModified = 3;
    [ObservableProperty] private double _gitUntracked = 1;
    [ObservableProperty] private double _gitAhead = 1;
    [ObservableProperty] private double _gitBehind = 0;
    [ObservableProperty] private double _gitLinesAdded = 42;
    [ObservableProperty] private double _gitLinesRemoved = 10;

    [ObservableProperty] private string _projectVersion = "1.4.2";
    [ObservableProperty] private double _columns = 160;

    public RenderContext Build()
    {
        var now = DateTimeOffset.Now;
        long contextSize = (long)(ContextSizeK * 1000);
        long totalInput = (long)(ContextPercent / 100.0 * contextSize);

        var input = new StatusInput
        {
            Cwd = Cwd,
            SessionId = "sample-session",
            SessionName = NullIfEmpty(SessionName),
            Version = NullIfEmpty(CcVersion),
            Model = new ModelInfo { Id = ModelId, DisplayName = ModelName },
            Workspace = new WorkspaceInfo
            {
                CurrentDir = Cwd,
                ProjectDir = Cwd,
                GitWorktree = NullIfEmpty(WorktreeName),
                Repo = string.IsNullOrEmpty(RepoName) ? null
                    : new RepoInfo { Host = "github.com", Owner = RepoOwner, Name = RepoName },
            },
            OutputStyle = new OutputStyleInfo { Name = NullIfEmpty(OutputStyle) },
            Cost = new CostInfo
            {
                TotalCostUsd = CostUsd,
                TotalDurationMs = DurationMin * 60_000,
                TotalApiDurationMs = ApiMin * 60_000,
                TotalLinesAdded = (long)LinesAdded,
                TotalLinesRemoved = (long)LinesRemoved,
            },
            ContextWindow = new ContextWindowInfo
            {
                TotalInputTokens = totalInput,
                TotalOutputTokens = (long)(TokensOutK * 1000),
                ContextWindowSize = contextSize,
                UsedPercentage = ContextPercent,
                RemainingPercentage = 100 - ContextPercent,
                CurrentUsage = new CurrentUsageInfo
                {
                    InputTokens = (long)(TokensInK * 1000),
                    OutputTokens = (long)(TokensOutK * 1000),
                    CacheReadInputTokens = (long)(TokensCachedK * 1000),
                    CacheCreationInputTokens = 0,
                },
            },
            Effort = string.IsNullOrEmpty(EffortLevel) ? null : new EffortInfo { Level = EffortLevel },
            Thinking = new ThinkingInfo { Enabled = ThinkingEnabled },
            RateLimits = !HasLimits ? null : new RateLimitsInfo
            {
                FiveHour = new RateWindowInfo
                {
                    UsedPercentage = Limit5h,
                    ResetsAt = now.AddHours(Limit5hResetHours).ToUnixTimeSeconds(),
                },
                SevenDay = new RateWindowInfo
                {
                    UsedPercentage = Limit7d,
                    ResetsAt = now.AddHours(Limit7dResetHours).ToUnixTimeSeconds(),
                },
            },
            Vim = string.IsNullOrEmpty(VimMode) ? null : new VimInfo { Mode = VimMode },
            Agent = string.IsNullOrEmpty(AgentName) ? null : new AgentInfo { Name = AgentName },
            Pr = !HasPr ? null : new PrInfo
            {
                Number = (long)PrNumber,
                Url = $"https://github.com/{RepoOwner}/{RepoName}/pull/{(long)PrNumber}",
                ReviewState = NullIfEmpty(PrState),
            },
        };

        var computed = new ComputedData
        {
            GitBranch = NullIfEmpty(GitBranch),
            GitStaged = (int)GitStaged,
            GitModified = (int)GitModified,
            GitUntracked = (int)GitUntracked,
            GitAhead = (int)GitAhead,
            GitBehind = (int)GitBehind,
            GitLinesAdded = (long)GitLinesAdded,
            GitLinesRemoved = (long)GitLinesRemoved,
            ProjectVersion = NullIfEmpty(ProjectVersion),
            Now = now,
            Columns = (int)Columns,
        };

        return new RenderContext { Input = input, Computed = computed };
    }

    static string? NullIfEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;
}
