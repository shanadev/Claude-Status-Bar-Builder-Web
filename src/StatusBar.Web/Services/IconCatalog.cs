// Hack the Claude Status Bar — lazy icons.json catalog for the icon browser.
// Copyright (C) 2026 Stephen Shanafelt
// SPDX-License-Identifier: GPL-3.0-only
//
// This program is free software: you can redistribute it and/or modify it under
// the terms of the GNU General Public License as published by the Free Software
// Foundation, version 3. It is distributed WITHOUT ANY WARRANTY; see the full
// license text in the LICENSE file at the root of this repository.

using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StatusBar.Web.Services;

public sealed class IconEntry
{
    [JsonPropertyName("c")] public string Char { get; set; } = "";
    [JsonPropertyName("n")] public string Name { get; set; } = "";
    [JsonPropertyName("k")] public string Keywords { get; set; } = "";
    [JsonPropertyName("g")] public string Group { get; set; } = "";

    [JsonIgnore] public bool IsNerdFont => Group.StartsWith("NF", StringComparison.Ordinal);
    [JsonIgnore] public bool IsEmoji => Group.StartsWith("Emoji", StringComparison.Ordinal);
}

/// <summary>Fetches wwwroot/icons.json once, on the browser's first open (872 KB — never eagerly).</summary>
public sealed class IconCatalog(HttpClient http)
{
    public IReadOnlyList<IconEntry>? Icons { get; private set; }
    Task<IReadOnlyList<IconEntry>>? _loading;

    public Task<IReadOnlyList<IconEntry>> EnsureLoadedAsync() =>
        _loading ??= LoadAsync();

    async Task<IReadOnlyList<IconEntry>> LoadAsync()
    {
        var icons = await http.GetFromJsonAsync<List<IconEntry>>("icons.json") ?? new();
        Icons = icons;
        return icons;
    }
}
