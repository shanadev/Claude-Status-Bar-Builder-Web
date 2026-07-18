// Claude Status Bar Builder — a visual designer for Claude Code status lines.
// Copyright (C) 2026 Stephen Shanafelt
// SPDX-License-Identifier: GPL-3.0-only
//
// This program is free software: you can redistribute it and/or modify it under
// the terms of the GNU General Public License as published by the Free Software
// Foundation, version 3. It is distributed WITHOUT ANY WARRANTY; see the full
// license text in the LICENSE file at the root of this repository.

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using StatusBar.Core;

// StatusBar.Renderer — the statusline command Claude Code runs.
// Reads the session JSON from stdin, loads a theme, gathers computed data (git etc.), prints ANSI rows.
// Usage: StatusBar.Renderer.exe [path-to-theme.json]

Console.OutputEncoding = Encoding.UTF8;
try { Console.InputEncoding = new UTF8Encoding(false); } catch { /* no console attached */ }

string themePath = args.FirstOrDefault(a => !a.StartsWith('-'))
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "statusbar-theme.json");
var theme = Theme.TryLoad(themePath) ?? DefaultTheme.Create();

// Claude Code always pipes session JSON into stdin. A human running the exe by hand
// has no pipe — don't sit waiting on stdin forever; show a demo + usage instead.
if (!Console.IsInputRedirected || args.Contains("--demo"))
{
    var sample = new SampleData();
    var demoCtx = sample.Build();
    try { demoCtx.Computed.Columns = Math.Max(60, Console.WindowWidth - 1); } catch { /* no console size */ }

    Console.Out.WriteLine("StatusBar.Renderer — the statusline command Claude Code runs.");
    Console.Out.WriteLine("It reads session JSON from stdin and prints ANSI status rows, so run by hand it");
    Console.Out.WriteLine("would normally wait forever. Here is your theme rendered with sample data:");
    Console.Out.WriteLine();
    foreach (var line in Composer.Compose(theme, demoCtx))
        if (line.Length > 0) Console.Out.WriteLine(line);
    Console.Out.WriteLine();
    Console.Out.WriteLine("Theme: " + themePath);
    Console.Out.WriteLine();
    Console.Out.WriteLine("To test with real input:  cmd /c \"type input.json | StatusBar.Renderer.exe theme.json\"");
    Console.Out.WriteLine("(cmd's type, not a PowerShell pipe — PS 5.1 re-encodes and mangles UTF-8.)");
    Console.Out.WriteLine("To use it in Claude Code: the Builder's 'Apply to Claude Code' button wires it up.");
    return;
}

string stdin = Console.In.ReadToEnd();
var input = StatusInput.Parse(stdin);

var computed = ComputedGatherer.Gather(theme, input);

var ctx = new RenderContext { Input = input, Computed = computed };
// Zero-length rows = every segment hid at runtime — drop them so the row collapses
// instead of leaving a blank line (deliberate blank rows compose to " " and survive).
foreach (var line in Composer.Compose(theme, ctx))
    if (line.Length > 0) Console.Out.WriteLine(line);

internal static class ComputedGatherer
{
    static readonly ElementKind[] GitKinds =
    {
        ElementKind.GitBranch, ElementKind.GitStatus, ElementKind.GitAheadBehind, ElementKind.GitDiff,
    };

    public static ComputedData Gather(Theme theme, StatusInput input)
    {
        var data = new ComputedData { Now = DateTimeOffset.Now };

        if (int.TryParse(Environment.GetEnvironmentVariable("COLUMNS"), out int cols) && cols > 20)
            data.Columns = cols;

        var kinds = theme.Rows.SelectMany(r => r.Segments).Select(s => s.Element).ToHashSet();
        string dir = input.Workspace?.CurrentDir ?? input.Cwd ?? Environment.CurrentDirectory;

        bool wantsGit = kinds.Overlaps(GitKinds);
        bool wantsVersion = kinds.Contains(ElementKind.ProjectVersion);
        if (!wantsGit && !wantsVersion) return data;

        // Git commands are slow-ish; cache per session+dir with a short TTL.
        string cacheKey = (input.SessionId ?? "nosession") + "|" + dir;
        string cachePath = Path.Combine(Path.GetTempPath(),
            "statusbar-cache-" + Math.Abs(cacheKey.GetHashCode()) + ".json");

        var cached = ReadCache(cachePath);
        if (cached is not null)
        {
            ApplyCache(cached, data);
            return data;
        }

        if (wantsGit) GatherGit(dir, data);
        if (wantsVersion) data.ProjectVersion = FindProjectVersion(input.Workspace?.ProjectDir ?? dir);
        WriteCache(cachePath, data);
        return data;
    }

    sealed class CacheEntry
    {
        public string? GitBranch { get; set; }
        public int GitStaged { get; set; }
        public int GitModified { get; set; }
        public int GitUntracked { get; set; }
        public int GitAhead { get; set; }
        public int GitBehind { get; set; }
        public long GitLinesAdded { get; set; }
        public long GitLinesRemoved { get; set; }
        public string? ProjectVersion { get; set; }
    }

    static CacheEntry? ReadCache(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || DateTime.UtcNow - info.LastWriteTimeUtc > TimeSpan.FromSeconds(5)) return null;
            return JsonSerializer.Deserialize<CacheEntry>(File.ReadAllText(path));
        }
        catch { return null; }
    }

    static void ApplyCache(CacheEntry c, ComputedData d)
    {
        d.GitBranch = c.GitBranch;
        d.GitStaged = c.GitStaged;
        d.GitModified = c.GitModified;
        d.GitUntracked = c.GitUntracked;
        d.GitAhead = c.GitAhead;
        d.GitBehind = c.GitBehind;
        d.GitLinesAdded = c.GitLinesAdded;
        d.GitLinesRemoved = c.GitLinesRemoved;
        d.ProjectVersion = c.ProjectVersion;
    }

    static void WriteCache(string path, ComputedData d)
    {
        try
        {
            var entry = new CacheEntry
            {
                GitBranch = d.GitBranch,
                GitStaged = d.GitStaged,
                GitModified = d.GitModified,
                GitUntracked = d.GitUntracked,
                GitAhead = d.GitAhead,
                GitBehind = d.GitBehind,
                GitLinesAdded = d.GitLinesAdded,
                GitLinesRemoved = d.GitLinesRemoved,
                ProjectVersion = d.ProjectVersion,
            };
            File.WriteAllText(path, JsonSerializer.Serialize(entry));
        }
        catch { /* cache is best-effort */ }
    }

    static void GatherGit(string dir, ComputedData data)
    {
        string? status = RunGit(dir, "status --porcelain=v2 --branch");
        if (status is null) return;

        foreach (var line in status.Split('\n'))
        {
            if (line.StartsWith("# branch.head "))
            {
                var head = line["# branch.head ".Length..].Trim();
                data.GitBranch = head == "(detached)" ? "detached" : head;
            }
            else if (line.StartsWith("# branch.ab "))
            {
                var m = Regex.Match(line, @"\+(\d+) -(\d+)");
                if (m.Success)
                {
                    data.GitAhead = int.Parse(m.Groups[1].Value);
                    data.GitBehind = int.Parse(m.Groups[2].Value);
                }
            }
            else if (line.StartsWith("1 ") || line.StartsWith("2 "))
            {
                // porcelain v2: "<1|2> XY ..." where X = staged state, Y = worktree state
                if (line.Length > 4)
                {
                    if (line[2] != '.') data.GitStaged++;
                    if (line[3] != '.') data.GitModified++;
                }
            }
            else if (line.StartsWith("? ")) data.GitUntracked++;
        }

        // Uncommitted line deltas (staged + unstaged)
        foreach (string diffArgs in new[] { "diff --numstat", "diff --cached --numstat" })
        {
            string? numstat = RunGit(dir, diffArgs);
            if (numstat is null) continue;
            foreach (var line in numstat.Split('\n'))
            {
                var parts = line.Split('\t');
                if (parts.Length >= 2
                    && long.TryParse(parts[0], out long added)
                    && long.TryParse(parts[1], out long removed))
                {
                    data.GitLinesAdded += added;
                    data.GitLinesRemoved += removed;
                }
            }
        }
    }

    static string? RunGit(string dir, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "-C \"" + dir + "\" " + arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
            };
            using var proc = Process.Start(psi);
            if (proc is null) return null;
            string output = proc.StandardOutput.ReadToEnd();
            if (!proc.WaitForExit(2000)) { try { proc.Kill(true); } catch { } return null; }
            return proc.ExitCode == 0 ? output : null;
        }
        catch { return null; }
    }

    static string? FindProjectVersion(string dir)
    {
        try
        {
            string packageJson = Path.Combine(dir, "package.json");
            if (File.Exists(packageJson))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(packageJson));
                if (doc.RootElement.TryGetProperty("version", out var v)) return v.GetString();
            }

            foreach (var csproj in Directory.EnumerateFiles(dir, "*.csproj", SearchOption.TopDirectoryOnly))
            {
                var m = Regex.Match(File.ReadAllText(csproj), @"<Version>([^<]+)</Version>");
                if (m.Success) return m.Groups[1].Value;
            }

            string pyproject = Path.Combine(dir, "pyproject.toml");
            if (File.Exists(pyproject))
            {
                var m = Regex.Match(File.ReadAllText(pyproject), @"(?m)^version\s*=\s*""([^""]+)""");
                if (m.Success) return m.Groups[1].Value;
            }
        }
        catch { }
        return null;
    }
}
