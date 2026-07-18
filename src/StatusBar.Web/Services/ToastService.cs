// Hack the Claude Status Bar — toast notifications (green confirm + flavor, red = no flavor).
// Copyright (C) 2026 Stephen Shanafelt
// SPDX-License-Identifier: GPL-3.0-only
//
// This program is free software: you can redistribute it and/or modify it under
// the terms of the GNU General Public License as published by the Free Software
// Foundation, version 3. It is distributed WITHOUT ANY WARRANTY; see the full
// license text in the LICENSE file at the root of this repository.

namespace StatusBar.Web.Services;

public sealed class ToastService
{
    public string? Title { get; private set; }
    public string? Flavor { get; private set; }
    public bool IsError { get; private set; }
    public event Action? Changed;
    CancellationTokenSource? _cts;

    /// <summary>Green confirm with one line of flavor. Errors are red and flavor-free.</summary>
    public void Show(string title, string? flavor = null, bool error = false)
    {
        Title = title;
        Flavor = error ? null : flavor;
        IsError = error;
        Changed?.Invoke();

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        _ = HideAfterDelayAsync(_cts.Token);
    }

    // No ContinueWith/FromCurrentSynchronizationContext here: Blazor WASM has no
    // SynchronizationContext, so that overload throws (the "unhandled error" banner
    // right after every toast). Plain await resumes on the single WASM thread anyway.
    async Task HideAfterDelayAsync(CancellationToken token)
    {
        try { await Task.Delay(2600, token); } catch (TaskCanceledException) { return; }
        Title = null;
        Changed?.Invoke();
    }
}
