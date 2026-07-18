// Hack the Claude Status Bar — site utilities (clipboard, downloads, storage, easter eggs).
// Copyright (C) 2026 Stephen Shanafelt
// SPDX-License-Identifier: GPL-3.0-only
//
// This program is free software: you can redistribute it and/or modify it under
// the terms of the GNU General Public License as published by the Free Software
// Foundation, version 3. It is distributed WITHOUT ANY WARRANTY; see the full
// license text in the LICENSE file at the root of this repository.

window.sbCopy = function (text) {
    return navigator.clipboard.writeText(text).then(function () { return true; }, function () { return false; });
};

window.sbDownload = function (filename, text, mime) {
    var blob = new Blob([text], { type: mime || 'application/json' });
    var a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(function () { URL.revokeObjectURL(a.href); }, 4000);
};

window.sbGet = function (key) {
    try { return localStorage.getItem(key); } catch (e) { return null; }
};

window.sbSet = function (key, value) {
    try {
        if (value === null) localStorage.removeItem(key);
        else localStorage.setItem(key, value);
    } catch (e) { }
};

window.sbReboot = function () {
    try { localStorage.removeItem('sbb-booted'); } catch (e) { }
    location.href = '/';
};

window.sbPlatform = function () {
    var p = (navigator.userAgentData && navigator.userAgentData.platform) || navigator.platform || '';
    p = p.toLowerCase();
    if (p.indexOf('win') >= 0) return 'windows';
    if (p.indexOf('mac') >= 0) return 'macos';
    return 'linux';
};

// "/" focuses the palette search from anywhere in the builder (not while typing).
document.addEventListener('keydown', function (e) {
    if (e.key !== '/' || e.ctrlKey || e.metaKey || e.altKey) return;
    var t = e.target;
    if (t && (t.tagName === 'INPUT' || t.tagName === 'TEXTAREA' || t.tagName === 'SELECT' || t.isContentEditable)) return;
    var box = document.getElementById('palette-search');
    if (box) { e.preventDefault(); box.focus(); }
});

// ── easter eggs: discoverable, never shouted ────────────────────────────────
console.log('%cHACK THE PLANET', 'color:#00ff66; font-size:20px; font-weight:bold; text-shadow:0 0 8px #00ff66;');
console.log('%cRISC architecture is gonna change everything. // theme lives in the URL after #t= — GPL-3.0, mess with the best.', 'color:#7ba08e;');

(function () {
    var code = ['ArrowUp', 'ArrowUp', 'ArrowDown', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'ArrowLeft', 'ArrowRight', 'b', 'a'];
    var at = 0;
    document.addEventListener('keydown', function (e) {
        at = (e.key === code[at]) ? at + 1 : (e.key === code[0] ? 1 : 0);
        if (at < code.length) return;
        at = 0;
        console.log('%cMESS WITH THE BEST, DIE LIKE THE REST', 'color:#ff2ea6; font-size:16px; font-weight:bold;');
        var word = document.querySelector('.topbar .word');
        if (word) {
            word.classList.add('glitch');
            word.setAttribute('data-text', word.textContent);
            setTimeout(function () { word.classList.remove('glitch'); }, 4500);
        }
    });
})();
