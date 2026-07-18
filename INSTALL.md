# Installing a status line from Hack the Claude Status Bar

This is the canonical install guide for themes built at the
[Hack the Claude Status Bar](https://github.com/shanadev/Claude-Status-Bar-Builder-Web) web builder.
It is written so that **Claude Code can perform the install itself** when a user pastes a theme
link and points it here — but every step works by hand too.

## What gets installed

| Piece | Where | What it is |
|---|---|---|
| Renderer | `~/.claude/statusbar/renderer` (`renderer.exe` on Windows) | Small self-contained binary. Claude Code pipes session JSON to it; it prints ANSI status rows. |
| Theme | `~/.claude/statusbar/theme.json` | The design built on the site. Plain JSON, edit freely. |
| Settings | `~/.claude/settings.json` → `statusLine` block | Tells Claude Code to run the renderer. |

Nothing else is touched. No telemetry, no network calls at runtime. GPL-3.0.

## 1. Decode the theme from the link

A theme link looks like `https://<site>/#t=1.XXXXXXXX…`. Everything after `#t=` is the theme:

- `1.` — format version prefix (currently always `1`)
- the rest — **base64url** (`-` → `+`, `_` → `/`, re-pad with `=`) of **raw-deflate-compressed** UTF-8 JSON

Python one-liner to decode (works everywhere Python 3 does):

```python
import base64, zlib
payload = "PASTE-EVERYTHING-AFTER-#t=1."  # the part after "1."
raw = base64.urlsafe_b64decode(payload + "=" * (-len(payload) % 4))
print(zlib.decompress(raw, wbits=-15).decode())  # -15 = raw deflate
```

Write the output to `~/.claude/statusbar/theme.json`.

The export screen's one-liners skip this step: they carry the theme as plain base64 JSON in an
`SBB_THEME` environment variable consumed by [`install.ps1`](https://github.com/shanadev/Claude-Status-Bar-Builder-Web/blob/main/src/StatusBar.Web/wwwroot/install.ps1)
/ [`install.sh`](https://github.com/shanadev/Claude-Status-Bar-Builder-Web/blob/main/src/StatusBar.Web/wwwroot/install.sh).

## 2. Get the renderer

Download the binary for the platform from the
[latest release](https://github.com/shanadev/Claude-Status-Bar-Builder-Web/releases/latest):

| Platform | Asset | Install as |
|---|---|---|
| Windows x64 | `StatusBar.Renderer-win-x64.exe` | `%USERPROFILE%\.claude\statusbar\renderer.exe` |
| macOS Apple Silicon | `StatusBar.Renderer-osx-arm64` | `~/.claude/statusbar/renderer` (`chmod +x`) |
| Linux x64 | `StatusBar.Renderer-linux-x64` | `~/.claude/statusbar/renderer` (`chmod +x`) |

No release binaries yet, or on another platform? Build from source (needs the .NET 10 SDK):

```
git clone https://github.com/shanadev/Claude-Status-Bar-Builder-Web
dotnet publish src/StatusBar.Renderer -c Release -r <win-x64|osx-arm64|linux-x64> --self-contained -p:PublishSingleFile=true
```

Sanity check: running `renderer --demo` in a terminal prints the theme with sample data.

## 3. Wire up Claude Code

**Back up `~/.claude/settings.json` first**, then merge in (keep all existing keys):

```jsonc
// macOS / Linux
{ "statusLine": { "type": "command", "command": "~/.claude/statusbar/renderer ~/.claude/statusbar/theme.json" } }
```

```jsonc
// Windows — absolute paths are the safe choice
{ "statusLine": { "type": "command", "command": "\"C:\\Users\\<you>\\.claude\\statusbar\\renderer.exe\" \"C:\\Users\\<you>\\.claude\\statusbar\\theme.json\"" } }
```

Restart Claude Code or start a new session; the status line appears at the bottom.

## Notes for Claude performing this install

- Ask before modifying `settings.json`; always create a timestamped backup next to it.
- If `settings.json` already has a `statusLine` block, show the user the old value before replacing.
- A Nerd Font in the user's terminal is needed for powerline caps/glyph icons. If the theme uses
  them and the user's terminal font isn't a Nerd Font, mention it — don't silently swap icons.
- The renderer needs no dependencies; it is self-contained. If it prints garbage instead of colors,
  the terminal probably lacks truecolor support.
- Uninstall = remove the `statusLine` block and delete `~/.claude/statusbar/`.
