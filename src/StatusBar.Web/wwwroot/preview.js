// Hack the Claude Status Bar — xterm.js preview bridge (web).
// Copyright (C) 2026 Stephen Shanafelt
// SPDX-License-Identifier: GPL-3.0-only
//
// This program is free software: you can redistribute it and/or modify it under
// the terms of the GNU General Public License as published by the Free Software
// Foundation, version 3. It is distributed WITHOUT ANY WARRANTY; see the full
// license text in the LICENSE file at the root of this repository.

let term = null, fitAddon = null;
const ESC = String.fromCharCode(27);

function ensureTerm(font, bg) {
    if (term) return;
    term = new Terminal({
        convertEol: true,
        disableStdin: true,
        cursorBlink: false,
        rows: 5,
        fontSize: 15,
        fontFamily: font,
        lineHeight: 1.15,
        theme: { background: bg },
        allowTransparency: false,
        // Emoji wider than their measured cell (🧠 etc.) otherwise get painted over
        // by the next cell's background — rescale them to fit instead of clipping.
        rescaleOverlappingGlyphs: true,
        scrollback: 0
    });
    fitAddon = new FitAddon.FitAddon();
    term.loadAddon(fitAddon);
    term.open(document.getElementById('term'));
    fitAddon.fit();
    window.addEventListener('resize', function () { fitAddon.fit(); });
}

// Called from Blazor. text = ANSI lines joined with \n. Returns current cols so C# can sync flex spacers.
window.renderStatus = async function (text, font, bg) {
    const stack = "'" + font + "', 'CaskaydiaCove NFM', 'Cascadia Code', Consolas, monospace";
    // Unlike the desktop WebView2 host, the font arrives over HTTP — wait for it
    // or xterm measures cells against a fallback and glyph widths come out wrong.
    try { await document.fonts.load("15px 'CaskaydiaCove NFM'"); } catch { /* render anyway */ }
    ensureTerm(stack, bg);
    if (term.options.fontFamily !== stack) term.options.fontFamily = stack;
    term.options.theme = { background: bg };
    const el = document.getElementById('term');
    if (el) el.style.background = bg;
    fitAddon.fit();
    term.reset();
    term.write(ESC + '[?25l' + text.split('\n').join('\r\n'));
    return term.cols;
};
