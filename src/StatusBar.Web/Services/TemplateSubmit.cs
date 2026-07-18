// Hack the Claude Status Bar — template submission: prefilled GitHub issue link.
// Copyright (C) 2026 Stephen Shanafelt
// SPDX-License-Identifier: GPL-3.0-only
//
// This program is free software: you can redistribute it and/or modify it under
// the terms of the GNU General Public License as published by the Free Software
// Foundation, version 3. It is distributed WITHOUT ANY WARRANTY; see the full
// license text in the LICENSE file at the root of this repository.

using StatusBar.Core;

namespace StatusBar.Web.Services;

/// <summary>
/// Template submissions are GitHub issues: the button below prefills the issue form
/// (.github/ISSUE_TEMPLATE/template-submission.yml) with the bar's name and share link.
/// Approval = a maintainer copies the entry into wwwroot/templates.json and pushes —
/// no backend, and the moderation queue is the issue tracker.
/// </summary>
public static class TemplateSubmit
{
    public const string RepoUrl = "https://github.com/shanadev/Claude-Status-Bar-Builder-Web";

    public static string IssueUrl(Theme theme, string baseUri) =>
        RepoUrl + "/issues/new?template=template-submission.yml"
        + "&title=" + Uri.EscapeDataString("[template] " + theme.Name)
        + "&bar-name=" + Uri.EscapeDataString(theme.Name)
        + "&share-link=" + Uri.EscapeDataString(baseUri + "#t=" + ThemeCodec.Encode(theme));
}
