// Hack the Claude Status Bar — icons.json catalog generator.
// Copyright (C) 2026 Stephen Shanafelt
// SPDX-License-Identifier: GPL-3.0-only
//
// This program is free software: you can redistribute it and/or modify it under
// the terms of the GNU General Public License as published by the Free Software
// Foundation, version 3. It is distributed WITHOUT ANY WARRANTY; see the full
// license text in the LICENSE file at the root of this repository.
//
// Rebuilds src/StatusBar.Web/wwwroot/icons.json from pinned upstream data:
//   Emoji   — emoji-test.txt (names, groups, order) + CLDR en annotations (keywords)
//   Unicode — curated symbol blocks below, names from UnicodeData.txt
//   NF      — nerd-fonts glyphnames.json (keys become names, prefixes become groups)
// Run:  node tools/gen-icons.js [cacheDir]     (sources download once into cacheDir,
// default <os tmp>/sbb-icons-cache — delete it to force a refresh).
//
// The pins reproduce the committed catalog byte-for-byte; bump them together with
// the bundled CaskaydiaCove font (glyphnames MUST match the font release or new
// entries render as tofu). Recipe notes that are easy to get wrong:
//   - emoji: fully-qualified rows only, rows whose name mentions "skin tone" skipped;
//     keywords are the CLDR phrases minus the one equal to the name (case-insensitive),
//     split on spaces, deduped in FIRST-SEEN order — CLDR's own order, never re-sorted.
//   - NF: name = key after the first hyphen, "-"/"_" become spaces; sorted by
//     (group, name); the indent- and indentation- prefixes share one group.

const path = require('path');
const fs = require('fs');
const os = require('os');

const SOURCES = {
    'emoji-test.txt': 'https://unicode.org/Public/17.0.0/emoji/emoji-test.txt',
    'annotations.json': 'https://raw.githubusercontent.com/unicode-org/cldr-json/48.2.0/cldr-json/cldr-annotations-full/annotations/en/annotations.json',
    'UnicodeData.txt': 'https://unicode.org/Public/17.0.0/ucd/UnicodeData.txt',
    'glyphnames.json': 'https://github.com/ryanoasis/nerd-fonts/raw/v3.4.0/glyphnames.json',
};

// Curated symbol blocks for the icon browser's "unicode" filter — broadly useful in
// a terminal, no font dependency. Everything else is reachable via paste-any-char.
const BLOCKS = [
    [0x2000, 0x206F, 'General Punctuation'],
    [0x20A0, 0x20BF, 'Currency Symbols'],
    [0x2100, 0x214F, 'Letterlike Symbols'],
    [0x2150, 0x218F, 'Number Forms'],
    [0x2190, 0x21FF, 'Arrows'],
    [0x2200, 0x22FF, 'Mathematical Operators'],
    [0x2300, 0x23FF, 'Miscellaneous Technical'],
    [0x2400, 0x243F, 'Control Pictures'],
    [0x2460, 0x24FF, 'Enclosed Alphanumerics'],
    [0x2500, 0x257F, 'Box Drawing'],
    [0x2580, 0x259F, 'Block Elements'],
    [0x25A0, 0x25FF, 'Geometric Shapes'],
    [0x2600, 0x26FF, 'Miscellaneous Symbols'],
    [0x2700, 0x27BF, 'Dingbats'],
    [0x2800, 0x28FF, 'Braille Patterns'],
    [0x2B00, 0x2BFF, 'Miscellaneous Symbols and Arrows'],
];

const NF_GROUPS = {
    cod: 'NF Codicons', custom: 'NF Custom', dev: 'NF Devicons', fae: 'NF FA Extended',
    fa: 'NF Font Awesome', linux: 'NF Font Logos', iec: 'NF IEC Power', indent: 'NF Indent',
    indentation: 'NF Indent', md: 'NF Material Design', oct: 'NF Octicons', pom: 'NF Pomicons',
    pl: 'NF Powerline', ple: 'NF Powerline Extra', seti: 'NF Seti UI', weather: 'NF Weather',
    extra: 'NF extra',
};

const outPath = path.join(__dirname, '..', 'src', 'StatusBar.Web', 'wwwroot', 'icons.json');
const cacheDir = process.argv[2] || path.join(os.tmpdir(), 'sbb-icons-cache');

async function fetchSources() {
    fs.mkdirSync(cacheDir, { recursive: true });
    const texts = {};
    for (const [name, url] of Object.entries(SOURCES)) {
        const file = path.join(cacheDir, name);
        if (!fs.existsSync(file)) {
            process.stdout.write('fetching ' + url + ' ... ');
            const res = await fetch(url);
            if (!res.ok) throw new Error(res.status + ' for ' + url);
            fs.writeFileSync(file, Buffer.from(await res.arrayBuffer()));
            console.log('ok');
        }
        texts[name] = fs.readFileSync(file, 'utf8');
    }
    return texts;
}

function buildEmoji(testTxt, annotationsJson) {
    const annMap = JSON.parse(annotationsJson).annotations.annotations;
    let group = '';
    const entries = [];
    for (const line of testTxt.split('\n')) {
        const gm = line.match(/^# group: (.+)/);
        if (gm) { group = gm[1].trim(); continue; }
        const m = line.match(/^([0-9A-F ]+?)\s*;\s*(\S[\S -]*?)\s*#\s*(\S+)\s+E[\d.]+\s+(.+)/);
        if (!m) continue;
        const [, , qual, ch, name] = m;
        if (qual !== 'fully-qualified') continue;
        if (name.includes('skin tone')) continue;
        const ann = annMap[ch] || annMap[ch.replace(/\uFE0F/g, '')]; // retry without VS16
        const words = new Set();
        if (ann && ann.default) {
            for (const phrase of ann.default) {
                if (phrase.toLowerCase() === name.toLowerCase()) continue;
                for (const w of phrase.split(/\s+/)) words.add(w);
            }
        }
        entries.push({ c: ch, n: name, k: [...words].join(' '), g: 'Emoji — ' + group });
    }
    return entries;
}

function buildUnicode(unicodeData, taken) {
    const byCp = new Map();
    for (const line of unicodeData.split('\n')) {
        const f = line.split(';');
        if (f.length > 2) byCp.set(parseInt(f[0], 16), { name: f[1], cat: f[2] });
    }
    const entries = [];
    for (const [start, end, block] of BLOCKS) {
        for (let cp = start; cp <= end; cp++) {
            const u = byCp.get(cp);
            if (!u) continue;                        // unassigned
            if ('CZM'.includes(u.cat[0])) continue;  // invisible: control/format/space/combining
            const ch = String.fromCodePoint(cp);
            // Already listed as an emoji (bare or VS16 form) or a nerd-font glyph
            // (IEC power symbols live at real codepoints) — don't list it twice.
            if (taken.has(ch) || taken.has(ch + '\uFE0F')) continue;
            entries.push({ c: ch, n: u.name.toLowerCase(), k: '', g: 'Unicode — ' + block });
        }
    }
    return entries;
}

function buildNf(glyphnamesJson) {
    const glyphs = JSON.parse(glyphnamesJson);
    const entries = [];
    for (const [key, val] of Object.entries(glyphs)) {
        if (key === 'METADATA') continue;
        const dash = key.indexOf('-');
        const g = NF_GROUPS[key.slice(0, dash)];
        if (!g) throw new Error('unmapped nerd-fonts prefix in key: ' + key);
        entries.push({ c: val.char, n: key.slice(dash + 1).replace(/[-_]/g, ' '), k: '', g });
    }
    entries.sort((a, b) => a.g < b.g ? -1 : a.g > b.g ? 1 : a.n < b.n ? -1 : a.n > b.n ? 1 : 0);
    return entries;
}

(async () => {
    const src = await fetchSources();
    const emoji = buildEmoji(src['emoji-test.txt'], src['annotations.json']);
    const nf = buildNf(src['glyphnames.json']);
    const unicode = buildUnicode(src['UnicodeData.txt'], new Set([...emoji, ...nf].map(e => e.c)));

    // Loud pin-drift alarms: these counts are exact for the pinned source versions.
    // (Emoji and NF sets share some chars between themselves — always have; only the
    // unicode section guarantees it introduces no duplicates.)
    if (emoji.length !== 1914) throw new Error('emoji count drifted: ' + emoji.length + ' (expected 1914)');
    if (nf.length !== 10764) throw new Error('NF count drifted: ' + nf.length + ' (expected 10764)');

    const all = [...emoji, ...unicode, ...nf];
    fs.writeFileSync(outPath, JSON.stringify(all), { encoding: 'utf8' }); // one line, LF, no BOM
    console.log('wrote ' + outPath);
    console.log('entries: emoji=' + emoji.length + ' unicode=' + unicode.length + ' nf=' + nf.length +
        ' total=' + all.length);
    const perBlock = {};
    for (const e of unicode) perBlock[e.g] = (perBlock[e.g] || 0) + 1;
    for (const [g, n] of Object.entries(perBlock)) console.log('  ' + g + ': ' + n);
})();
