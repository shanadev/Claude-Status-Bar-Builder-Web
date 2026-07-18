# Hack the Claude Status Bar — Windows installer.
# Copyright (C) 2026 Stephen Shanafelt
# SPDX-License-Identifier: GPL-3.0-only
#
# What this does — nothing else:
#   1. downloads the status line renderer to  ~\.claude\statusbar\renderer.exe
#   2. writes your theme (from $env:SBB_THEME, base64 JSON) to  ~\.claude\statusbar\theme.json
#   3. backs up  ~\.claude\settings.json  then adds/replaces its "statusLine" block
#
# Run it from the export screen one-liner, or read it first — that's the point of hosting it.

$ErrorActionPreference = 'Stop'

$repo = 'https://github.com/shanadev/Claude-Status-Bar-Builder-Web'
$dir = Join-Path $HOME '.claude\statusbar'
$renderer = Join-Path $dir 'renderer.exe'
$themePath = Join-Path $dir 'theme.json'
$settingsPath = Join-Path $HOME '.claude\settings.json'

New-Item -ItemType Directory -Force -Path $dir | Out-Null

# 1 — renderer
Write-Host '> downloading renderer (win-x64)...' -ForegroundColor Cyan
try {
    Invoke-WebRequest -UseBasicParsing "$repo/releases/latest/download/StatusBar.Renderer-win-x64.exe" -OutFile $renderer
} catch {
    Write-Host 'x renderer download failed — no release binaries yet?' -ForegroundColor Red
    Write-Host "  Build it from source instead:  git clone $repo ; dotnet publish src/StatusBar.Renderer -c Release -r win-x64 --self-contained -p:PublishSingleFile=true" -ForegroundColor Yellow
    Write-Host "  Then copy the exe to $renderer and re-run this script."
    exit 1
}

# 2 — theme
if ($env:SBB_THEME) {
    $json = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($env:SBB_THEME))
    [IO.File]::WriteAllText($themePath, $json, (New-Object Text.UTF8Encoding($false)))
    Write-Host "> theme written to $themePath" -ForegroundColor Cyan
} elseif (Test-Path $themePath) {
    Write-Host "> keeping existing $themePath" -ForegroundColor Cyan
} else {
    Write-Host '> no SBB_THEME provided — the renderer will use its built-in default theme' -ForegroundColor Yellow
}

# 3 — settings.json (backup first)
$settings = New-Object PSObject
if (Test-Path $settingsPath) {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    Copy-Item $settingsPath "$settingsPath.backup-$stamp"
    Write-Host "> settings.json backed up to settings.json.backup-$stamp" -ForegroundColor Cyan
    $settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
}
$statusLine = New-Object PSObject
$statusLine | Add-Member NoteProperty type 'command'
$statusLine | Add-Member NoteProperty command "`"$renderer`" `"$themePath`""
$settings | Add-Member NoteProperty statusLine $statusLine -Force
[IO.File]::WriteAllText($settingsPath, ($settings | ConvertTo-Json -Depth 20), (New-Object Text.UTF8Encoding($false)))

Write-Host ''
Write-Host 'ACCESS GRANTED — status line installed.' -ForegroundColor Green
Write-Host 'Restart Claude Code (or start a new session) to see it. Mess with the best.'
