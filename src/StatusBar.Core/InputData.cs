// Claude Status Bar Builder — a visual designer for Claude Code status lines.
// Copyright (C) 2026 Stephen Shanafelt
// SPDX-License-Identifier: GPL-3.0-only
//
// This program is free software: you can redistribute it and/or modify it under
// the terms of the GNU General Public License as published by the Free Software
// Foundation, version 3. It is distributed WITHOUT ANY WARRANTY; see the full
// license text in the LICENSE file at the root of this repository.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace StatusBar.Core;

/// <summary>
/// The JSON payload Claude Code pipes to the statusline command's stdin.
/// Field names/shape per https://code.claude.com/docs/en/statusline (2026-07).
/// Everything is nullable: many fields are absent or null depending on session state.
/// </summary>
public sealed class StatusInput
{
    [JsonPropertyName("cwd")] public string? Cwd { get; set; }
    [JsonPropertyName("session_id")] public string? SessionId { get; set; }
    [JsonPropertyName("session_name")] public string? SessionName { get; set; }
    [JsonPropertyName("transcript_path")] public string? TranscriptPath { get; set; }
    [JsonPropertyName("version")] public string? Version { get; set; }
    [JsonPropertyName("model")] public ModelInfo? Model { get; set; }
    [JsonPropertyName("workspace")] public WorkspaceInfo? Workspace { get; set; }
    [JsonPropertyName("output_style")] public OutputStyleInfo? OutputStyle { get; set; }
    [JsonPropertyName("cost")] public CostInfo? Cost { get; set; }
    [JsonPropertyName("context_window")] public ContextWindowInfo? ContextWindow { get; set; }
    [JsonPropertyName("exceeds_200k_tokens")] public bool? Exceeds200k { get; set; }
    [JsonPropertyName("effort")] public EffortInfo? Effort { get; set; }
    [JsonPropertyName("thinking")] public ThinkingInfo? Thinking { get; set; }
    [JsonPropertyName("rate_limits")] public RateLimitsInfo? RateLimits { get; set; }
    [JsonPropertyName("vim")] public VimInfo? Vim { get; set; }
    [JsonPropertyName("agent")] public AgentInfo? Agent { get; set; }
    [JsonPropertyName("pr")] public PrInfo? Pr { get; set; }
    [JsonPropertyName("worktree")] public WorktreeInfo? Worktree { get; set; }

    public static StatusInput Parse(string json)
    {
        json = json.TrimStart('\uFEFF', ' ', '\r', '\n', '\t');
        try { return JsonSerializer.Deserialize<StatusInput>(json) ?? new StatusInput(); }
        catch { return new StatusInput(); }
    }
}

public sealed class ModelInfo
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("display_name")] public string? DisplayName { get; set; }
}

public sealed class WorkspaceInfo
{
    [JsonPropertyName("current_dir")] public string? CurrentDir { get; set; }
    [JsonPropertyName("project_dir")] public string? ProjectDir { get; set; }
    [JsonPropertyName("added_dirs")] public List<string>? AddedDirs { get; set; }
    [JsonPropertyName("git_worktree")] public string? GitWorktree { get; set; }
    [JsonPropertyName("repo")] public RepoInfo? Repo { get; set; }
}

public sealed class RepoInfo
{
    [JsonPropertyName("host")] public string? Host { get; set; }
    [JsonPropertyName("owner")] public string? Owner { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
}

public sealed class OutputStyleInfo
{
    [JsonPropertyName("name")] public string? Name { get; set; }
}

public sealed class CostInfo
{
    [JsonPropertyName("total_cost_usd")] public double? TotalCostUsd { get; set; }
    [JsonPropertyName("total_duration_ms")] public double? TotalDurationMs { get; set; }
    [JsonPropertyName("total_api_duration_ms")] public double? TotalApiDurationMs { get; set; }
    [JsonPropertyName("total_lines_added")] public long? TotalLinesAdded { get; set; }
    [JsonPropertyName("total_lines_removed")] public long? TotalLinesRemoved { get; set; }
}

public sealed class ContextWindowInfo
{
    [JsonPropertyName("total_input_tokens")] public long? TotalInputTokens { get; set; }
    [JsonPropertyName("total_output_tokens")] public long? TotalOutputTokens { get; set; }
    [JsonPropertyName("context_window_size")] public long? ContextWindowSize { get; set; }
    [JsonPropertyName("used_percentage")] public double? UsedPercentage { get; set; }
    [JsonPropertyName("remaining_percentage")] public double? RemainingPercentage { get; set; }
    [JsonPropertyName("current_usage")] public CurrentUsageInfo? CurrentUsage { get; set; }
}

public sealed class CurrentUsageInfo
{
    [JsonPropertyName("input_tokens")] public long? InputTokens { get; set; }
    [JsonPropertyName("output_tokens")] public long? OutputTokens { get; set; }
    [JsonPropertyName("cache_creation_input_tokens")] public long? CacheCreationInputTokens { get; set; }
    [JsonPropertyName("cache_read_input_tokens")] public long? CacheReadInputTokens { get; set; }
}

public sealed class EffortInfo
{
    [JsonPropertyName("level")] public string? Level { get; set; }
}

public sealed class ThinkingInfo
{
    [JsonPropertyName("enabled")] public bool? Enabled { get; set; }
}

public sealed class RateLimitsInfo
{
    [JsonPropertyName("five_hour")] public RateWindowInfo? FiveHour { get; set; }
    [JsonPropertyName("seven_day")] public RateWindowInfo? SevenDay { get; set; }
}

public sealed class RateWindowInfo
{
    [JsonPropertyName("used_percentage")] public double? UsedPercentage { get; set; }
    [JsonPropertyName("resets_at")] public long? ResetsAt { get; set; }
}

public sealed class VimInfo
{
    [JsonPropertyName("mode")] public string? Mode { get; set; }
}

public sealed class AgentInfo
{
    [JsonPropertyName("name")] public string? Name { get; set; }
}

public sealed class PrInfo
{
    [JsonPropertyName("number")] public long? Number { get; set; }
    [JsonPropertyName("url")] public string? Url { get; set; }
    [JsonPropertyName("review_state")] public string? ReviewState { get; set; }
}

public sealed class WorktreeInfo
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("path")] public string? Path { get; set; }
    [JsonPropertyName("branch")] public string? Branch { get; set; }
    [JsonPropertyName("original_cwd")] public string? OriginalCwd { get; set; }
    [JsonPropertyName("original_branch")] public string? OriginalBranch { get; set; }
}

/// <summary>Data the statusline JSON does NOT provide — gathered by the renderer (git, clock, etc.).</summary>
public sealed class ComputedData
{
    public string? GitBranch { get; set; }
    public int GitStaged { get; set; }
    public int GitModified { get; set; }
    public int GitUntracked { get; set; }
    public int GitAhead { get; set; }
    public int GitBehind { get; set; }
    public int GitStash { get; set; }
    public long GitLinesAdded { get; set; }
    public long GitLinesRemoved { get; set; }
    public string? ProjectVersion { get; set; }
    public DateTimeOffset Now { get; set; } = DateTimeOffset.Now;
    public int Columns { get; set; } = 160;
}

/// <summary>Everything a segment needs to render.</summary>
public sealed class RenderContext
{
    public required StatusInput Input { get; init; }
    public required ComputedData Computed { get; init; }
}
