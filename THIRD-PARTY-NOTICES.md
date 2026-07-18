# Third-Party Notices

Claude Status Bar Builder is licensed under the GNU General Public License v3.0
(see [LICENSE](LICENSE)). It bundles or derives from the third-party components
below, which remain under their own licenses. All of these licenses are
GPL-compatible; the combined work is distributed under the GPL-3.0.

---

## xterm.js

Files: `src/StatusBar.Builder/Assets/xterm/xterm.js`, `xterm.css`, `addon-fit.js`
(minified builds of [xterm.js](https://github.com/xtermjs/xterm.js) and
`@xterm/addon-fit`, used for the live terminal preview).

License: MIT

> Copyright (c) 2017-2022, The xterm.js authors (https://github.com/xtermjs/xterm.js)
> Copyright (c) 2014-2016, SourceLair Private Company (https://www.sourcelair.com)
>
> Permission is hereby granted, free of charge, to any person obtaining a copy
> of this software and associated documentation files (the "Software"), to deal
> in the Software without restriction, including without limitation the rights
> to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
> copies of the Software, and to permit persons to whom the Software is
> furnished to do so, subject to the following conditions:
>
> The above copyright notice and this permission notice shall be included in
> all copies or substantial portions of the Software.
>
> THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
> IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
> FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
> AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
> LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
> OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
> THE SOFTWARE.

---

## CaskaydiaCove Nerd Font Mono

File: `src/StatusBar.Builder/Assets/fonts/CaskaydiaCoveNerdFontMono-Regular.ttf`
(used by the preview terminal so Nerd Font glyphs render correctly).

This is [Cascadia Code](https://github.com/microsoft/cascadia-code)
(Copyright (c) 2019 – present, Microsoft Corporation), patched with additional
glyphs by the [Nerd Fonts](https://github.com/ryanoasis/nerd-fonts) project and
renamed per the OFL's Reserved Font Name rules.

- Cascadia Code is licensed under the **SIL Open Font License 1.1** (full text
  below).
- The Nerd Fonts patcher and project assets are MIT licensed,
  Copyright (c) 2014 Ryan L McIntyre.
- Glyphs added by patching originate from various icon fonts; see the
  [Nerd Fonts license notes](https://github.com/ryanoasis/nerd-fonts#tada-license)
  for the per-source breakdown.

### SIL Open Font License, Version 1.1

> Copyright (c) 2019 - Present, Microsoft Corporation,
> with Reserved Font Name Cascadia Code.
>
> This Font Software is licensed under the SIL Open Font License, Version 1.1.
> This license is copied below, and is also available with a FAQ at:
> https://scripts.sil.org/OFL
>
> -----------------------------------------------------------
> SIL OPEN FONT LICENSE Version 1.1 - 26 February 2007
> -----------------------------------------------------------
>
> PREAMBLE
> The goals of the Open Font License (OFL) are to stimulate worldwide
> development of collaborative font projects, to support the font creation
> efforts of academic and linguistic communities, and to provide a free and
> open framework in which fonts may be shared and improved in partnership
> with others.
>
> The OFL allows the licensed fonts to be used, studied, modified and
> redistributed freely as long as they are not sold by themselves. The
> fonts, including any derivative works, can be bundled, embedded,
> redistributed and/or sold with any software provided that any reserved
> names are not used by derivative works. The fonts and derivatives,
> however, cannot be released under any other type of license. The
> requirement for fonts to remain under this license does not apply
> to any document created using the fonts or their derivatives.
>
> DEFINITIONS
> "Font Software" refers to the set of files released by the Copyright
> Holder(s) under this license and clearly marked as such. This may
> include source files, build scripts and documentation.
>
> "Reserved Font Name" refers to any names specified as such after the
> copyright statement(s).
>
> "Original Version" refers to the collection of Font Software components as
> distributed by the Copyright Holder(s).
>
> "Modified Version" refers to any derivative made by adding to, deleting,
> or substituting -- in part or in whole -- any of the components of the
> Original Version, by changing formats or by porting the Font Software to a
> new environment.
>
> "Author" refers to any designer, engineer, programmer, technical
> writer or other person who contributed to the Font Software.
>
> PERMISSION & CONDITIONS
> Permission is hereby granted, free of charge, to any person obtaining
> a copy of the Font Software, to use, study, copy, merge, embed, modify,
> redistribute, and sell modified and unmodified copies of the Font
> Software, subject to the following conditions:
>
> 1) Neither the Font Software nor any of its individual components,
> in Original or Modified Versions, may be sold by itself.
>
> 2) Original or Modified Versions of the Font Software may be bundled,
> redistributed and/or sold with any software, provided that each copy
> contains the above copyright notice and this license. These can be
> included either as stand-alone text files, human-readable headers or
> in the appropriate machine-readable metadata fields within text or
> binary files as long as those fields can be easily viewed by the user.
>
> 3) No Modified Version of the Font Software may use the Reserved Font
> Name(s) unless explicit written permission is granted by the corresponding
> Copyright Holder. This restriction only applies to the primary font name as
> presented to the users.
>
> 4) The name(s) of the Copyright Holder(s) or the Author(s) of the Font
> Software shall not be used to promote, endorse or advertise any
> Modified Version, except to acknowledge the contribution(s) of the
> Copyright Holder(s) and the Author(s) or with their explicit written
> permission.
>
> 5) The Font Software, modified or unmodified, in part or in whole,
> must be distributed entirely under this license, and must not be
> distributed under any other license. The requirement for fonts to
> remain under this license does not apply to any document created
> using the Font Software.
>
> TERMINATION
> This license becomes null and void if any of the above conditions are
> not met.
>
> DISCLAIMER
> THE FONT SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
> EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO ANY WARRANTIES OF
> MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT
> OF COPYRIGHT, PATENT, TRADEMARK, OR OTHER RIGHT. IN NO EVENT SHALL THE
> COPYRIGHT HOLDER BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
> INCLUDING ANY GENERAL, SPECIAL, INDIRECT, INCIDENTAL, OR CONSEQUENTIAL
> DAMAGES, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
> FROM, OUT OF THE USE OR INABILITY TO USE THE FONT SOFTWARE OR FROM
> OTHER DEALINGS IN THE FONT SOFTWARE.

---

## Icon metadata (`Assets/icons.json`)

The searchable icon index is generated from:

- **Nerd Fonts `glyphnames.json`** — official glyph names for the Nerd Fonts
  icon sets. MIT licensed, Copyright (c) 2014 Ryan L McIntyre
  (https://github.com/ryanoasis/nerd-fonts).
- **Unicode® emoji data** (`emoji-test.txt`) and **Unicode CLDR** annotation
  keywords (English). Copyright © Unicode, Inc. Distributed under the
  [UNICODE LICENSE V3](https://www.unicode.org/license.txt).
  Unicode and the Unicode Logo are registered trademarks of Unicode, Inc.

---

## NuGet packages (fetched at build time, not vendored in this repository)

- **CommunityToolkit.Mvvm** — MIT, Copyright © .NET Foundation and Contributors.
- **Microsoft.Web.WebView2** — Microsoft license; the WebView2 loader is
  redistributed with builds per its license terms.
