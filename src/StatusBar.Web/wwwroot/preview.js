// Hack the Claude Status Bar — xterm.js preview bridge (web).
// Copyright (C) 2026 Stephen Shanafelt
// SPDX-License-Identifier: GPL-3.0-only
//
// This program is free software: you can redistribute it and/or modify it under
// the terms of the GNU General Public License as published by the Free Software
// Foundation, version 3. It is distributed WITHOUT ANY WARRANTY; see the full
// license text in the LICENSE file at the root of this repository.

let term = null, fitAddon = null, webglAddon = null;
let colsMode = 'fit'; // 'fit' tracks the panel width; a number pins the column count
let resizeRef = null, resizeTimer = null;
const ESC = String.fromCharCode(27);

// One resize listener for the lifetime of the page — ensureTerm can run more than
// once (see below), so registration must not live inside it.
window.addEventListener('resize', function () {
    if (colsMode !== 'fit' || !resizeRef) return;
    clearTimeout(resizeTimer);
    resizeTimer = setTimeout(function () {
        resizeRef.invokeMethodAsync('OnHostResize');
    }, 150);
});

function ensureTerm(font, bg) {
    if (term) {
        // Navigating away destroys the #term div Blazor rendered; the xterm instance
        // keeps writing into that detached node forever (the "dead preview" after
        // export → build). Detect it and rebuild against the fresh div.
        if (term.element && term.element.isConnected) return;
        try { if (webglAddon) webglAddon.dispose(); } catch (e) { }
        try { term.dispose(); } catch (e) { }
        term = null; fitAddon = null; webglAddon = null;
    }
    term = new Terminal({
        // The unicode11 addon registers its provider through xterm's proposed API.
        allowProposedApi: true,
        convertEol: true,
        disableStdin: true,
        cursorBlink: false,
        rows: 2,
        fontSize: 15,
        fontFamily: font,
        lineHeight: 1.15,
        theme: { background: bg },
        allowTransparency: false,
        // Glyphs wider than their cell (emoji, big NF icons) otherwise get painted
        // over by the next cell's background — rescale them to fit instead of
        // clipping. Only the WebGL renderer honors these; the DOM renderer ignores
        // them and half-hides the glyph, hence the addon load below.
        rescaleOverlappingGlyphs: true,
        customGlyphs: true,
        scrollback: 0
    });
    fitAddon = new FitAddon.FitAddon();
    term.loadAddon(fitAddon);
    // Unicode 11 widths: emoji occupy 2 cells like real terminals. Core's Fmt.Width
    // mirrors these exact tables (UnicodeWidth.cs) — alignment math depends on it.
    term.loadAddon(new Unicode11Addon.Unicode11Addon());
    term.unicode.activeVersion = '11';
    term.open(document.getElementById('term'));
    window.sbbTerm = term; // test hook: verification scripts assert on the cell buffer
    try {
        webglAddon = new WebglAddon.WebglAddon();
        webglAddon.onContextLoss(function () {
            webglAddon.dispose(); // falls back to the DOM renderer
            webglAddon = null;
        });
        term.loadAddon(webglAddon);
    } catch (e) {
        webglAddon = null; // no WebGL2 — DOM renderer still works, icons just clip
    }
}

function currentCols() {
    if (colsMode !== 'fit') return colsMode;
    const d = fitAddon.proposeDimensions();
    // A collapsed/unmeasured container proposes nonsense — keep the current width then.
    if (!d || !isFinite(d.cols) || d.cols < 20) return term.cols;
    return d.cols;
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
    const lines = text.split('\n');
    const cols = currentCols();
    if (term.cols !== cols || term.rows !== lines.length) term.resize(cols, lines.length);
    term.reset();
    // ?25l hides the cursor; ?7l disables autowrap — a too-wide line clips at the right
    // edge exactly like a real terminal status line instead of wrapping out of view.
    // The write MUST be awaited: reset() is synchronous but write() is buffered, so an
    // unawaited write lets the next render's reset run first and both payloads land on
    // the same row (the status line shows doubled).
    await new Promise(function (resolve) {
        term.write(ESC + '[?25l' + ESC + '[?7l' + lines.join('\r\n'), resolve);
    });
    return term.cols;
};

// Width presets: 80 / 120 / 160 pin the terminal; 'fit' tracks the panel. Returns resulting cols.
window.setPreviewCols = function (mode) {
    colsMode = (mode === 'fit') ? 'fit' : parseInt(mode, 10);
    if (!term) return 0;
    return currentCols();
};

// Blazor registers here to be recomposed when a window resize changes the fitted width.
window.watchPreviewResize = function (dotnetRef) { resizeRef = dotnetRef; };
window.unwatchPreviewResize = function () { resizeRef = null; };
