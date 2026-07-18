#!/usr/bin/env bash
# Hack the Claude Status Bar — macOS / Linux installer.
# Copyright (C) 2026 Stephen Shanafelt
# SPDX-License-Identifier: GPL-3.0-only
#
# What this does — nothing else:
#   1. downloads the status line renderer to  ~/.claude/statusbar/renderer
#   2. writes your theme (from $SBB_THEME, base64 JSON) to  ~/.claude/statusbar/theme.json
#   3. backs up  ~/.claude/settings.json  then adds/replaces its "statusLine" block
#
# Run it from the export screen one-liner, or read it first — that's the point of hosting it.

set -euo pipefail

repo='https://github.com/shanadev/Claude-Status-Bar-Builder-Web'
dir="$HOME/.claude/statusbar"
renderer="$dir/renderer"
theme="$dir/theme.json"
settings="$HOME/.claude/settings.json"

case "$(uname -s)" in
    Darwin) asset='StatusBar.Renderer-osx-arm64'; rid='osx-arm64' ;;
    *)      asset='StatusBar.Renderer-linux-x64'; rid='linux-x64' ;;
esac

mkdir -p "$dir"

# 1 — renderer
echo "> downloading renderer ($rid)..."
if ! curl -fSL "$repo/releases/latest/download/$asset" -o "$renderer"; then
    echo 'x renderer download failed — no release binaries yet?'
    echo "  Build it from source instead:  git clone $repo && dotnet publish src/StatusBar.Renderer -c Release -r $rid --self-contained -p:PublishSingleFile=true"
    echo "  Then copy the binary to $renderer and re-run this script."
    exit 1
fi
chmod +x "$renderer"

# 2 — theme
if [ -n "${SBB_THEME:-}" ]; then
    printf '%s' "$SBB_THEME" | openssl base64 -d -A > "$theme"
    echo "> theme written to $theme"
elif [ -f "$theme" ]; then
    echo "> keeping existing $theme"
else
    echo '> no SBB_THEME provided — the renderer will use its built-in default theme'
fi

# 3 — settings.json (backup first; python3 does the JSON surgery)
if ! command -v python3 >/dev/null 2>&1; then
    echo 'x python3 not found — add this to ~/.claude/settings.json yourself:'
    printf '{\n  "statusLine": {\n    "type": "command",\n    "command": "%s %s"\n  }\n}\n' "$renderer" "$theme"
    exit 1
fi
if [ -f "$settings" ]; then
    stamp="$(date +%Y%m%d-%H%M%S)"
    cp "$settings" "$settings.backup-$stamp"
    echo "> settings.json backed up to settings.json.backup-$stamp"
fi
RENDERER="$renderer" THEME="$theme" SETTINGS="$settings" python3 - <<'PYEOF'
import json, os
path = os.environ['SETTINGS']
data = {}
if os.path.exists(path):
    with open(path, encoding='utf-8') as f:
        data = json.load(f)
data['statusLine'] = {
    'type': 'command',
    'command': f"{os.environ['RENDERER']} {os.environ['THEME']}",
}
with open(path, 'w', encoding='utf-8') as f:
    json.dump(data, f, indent=2)
    f.write('\n')
PYEOF

echo
echo 'ACCESS GRANTED — status line installed.'
echo 'Restart Claude Code (or start a new session) to see it. Mess with the best.'
