using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Changelog;

/// <summary>One release with its visible lines, as the page renders it.</summary>
public sealed record ChangelogReleaseView(
    string Id,
    string Version,
    DateTime Date,
    string? Title,
    string? BuildNumber,
    IReadOnlyList<ChangelogEntryView> Entries);

/// <summary>One line of a release.</summary>
public sealed record ChangelogEntryView(string Id, ChangelogKind Kind, string Title, string? Area);

/// <summary>Editable fields of a release.</summary>
public sealed record ChangelogReleaseInput(
    string Version,
    DateTime Date,
    string? Title,
    int SortOrder,
    bool IsVisible);

/// <summary>Editable fields of a line.</summary>
public sealed record ChangelogEntryInput(
    string ReleaseId,
    ChangelogKind Kind,
    string Title,
    string? Area,
    int SortOrder,
    bool IsVisible);

/// <summary>What the login hint needs: how many lines are new, and which release they belong to.</summary>
/// <param name="Count">Visible lines in releases that arrived after the agent last looked.</param>
/// <param name="NewestVersion">Newest release involved, for the wording of the card.</param>
public sealed record ChangelogNewsFlash(int Count, string? NewestVersion);
