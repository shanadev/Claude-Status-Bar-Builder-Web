// Hack the Claude Status Bar - blank-line probe: which spacer characters survive
// Claude Code's statusline display? Point statusLine.command at this script and
// eyeball the result: each "gap above?" marker reports on the spacer line above it.
// Candidates are ordered most-likely-to-win first in case the display truncates.
// Copyright (C) 2026 Stephen Shanafelt
// SPDX-License-Identifier: GPL-3.0-only

// All non-ASCII is built at runtime so this source stays pure ASCII.
const ESC = String.fromCharCode(27);
const candidates = [
  ['1 braille blank U+2800', String.fromCharCode(0x2800)],
  ['2 zero-width space U+200B', String.fromCharCode(0x200b)],
  ['3 bare ANSI reset', ESC + '[0m'],
  ['4 nbsp U+00A0', String.fromCharCode(0xa0)],
  ['5 plain space (control: known to fail)', ' '],
  ['6 truly empty (control: known to fail)', ''],
];

let ran = false;
function run() {
  if (ran) return;
  ran = true;
  const out = ['=== blank-line probe / top ==='];
  for (const [name, spacer] of candidates) {
    out.push(spacer);
    out.push('gap above? then [' + name + '] works');
  }
  process.stdout.write(out.join('\n') + '\n');
  process.exit(0);
}

// Claude Code pipes session JSON on stdin and closes it; drain and go. The timer
// covers manual runs from a terminal where stdin never closes.
process.stdin.resume();
process.stdin.on('data', () => {});
process.stdin.on('end', run);
setTimeout(run, 150);
