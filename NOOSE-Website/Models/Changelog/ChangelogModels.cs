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

/// <summary>What arrived since the agent last looked, as the update prompt shows it.</summary>
/// <param name="Count">Visible lines that arrived after the agent last looked.</param>
/// <param name="NewestVersion">Newest release carrying one of them, for the heading of the prompt.</param>
/// <param name="Releases">Those lines under their releases, newest release first; within one, latest update first.</param>
public sealed record ChangelogNewsFlash(int Count, string? NewestVersion, IReadOnlyList<ChangelogReleaseView> Releases)
{
    /// <summary>Lines the prompt names before pointing at the full page.</summary>
    public const int PreviewLines = 4;

    public static ChangelogNewsFlash None { get; } = new(0, null, []);

    /// <summary>The first max lines in reading order, each still under its release.</summary>
    public IReadOnlyList<ChangelogReleaseView> Preview(int max = PreviewLines)
    {
        var left = Math.Max(0, max);
        var result = new List<ChangelogReleaseView>();
        foreach (var release in Releases)
        {
            if (left == 0)
            {
                break;
            }
            var taken = release.Entries.Take(left).ToList();
            if (taken.Count == 0)
            {
                continue;
            }
            result.Add(release with { Entries = taken });
            left -= taken.Count;
        }
        return result;
    }

    /// <summary>Lines left out of <see cref="Preview"/>.</summary>
    public int Hidden(int max = PreviewLines) => Math.Max(0, Count - Math.Max(0, max));
}
