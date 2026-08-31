using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Public;

/// <summary>A published release on the hub. Outward: carries no author, no record and no internal id.</summary>
public sealed record PublicPressCard(string CaseNumber, string Title, string Teaser, DateTime? PublishedAt);

/// <summary>A published release in full.</summary>
public sealed record PublicPressView(string CaseNumber, string Title, string Teaser, string Html, DateTime? PublishedAt);

/// <summary>Everything the public press pages read, cached as one unit.</summary>
public sealed record PublicPressSnapshot(
    IReadOnlyList<PublicPressCard> Cards,
    IReadOnlyDictionary<string, PublicPressView> ByCaseNumber)
{
    public static PublicPressSnapshot Empty { get; } =
        new([], new Dictionary<string, PublicPressView>(StringComparer.OrdinalIgnoreCase));

    public PublicPressView? Find(string? caseNumber)
        => caseNumber is not null && ByCaseNumber.TryGetValue(caseNumber, out var view) ? view : null;
}

/// <summary>Editing row of the settings panel.</summary>
/// <remarks>
/// Carries no HTML on purpose, same reason as PublicPageEdit: a release holds its pictures as base64 inside the body,
/// so a list row with the draft attached would pull every release's megabytes to render a table of titles. The editor
/// asks for the one draft it is about to show.
/// </remarks>
/// <param name="DraftDiffers">Draft and published copy differ, so publishing would change what visitors read.</param>
public sealed record PressEdit(
    string Id,
    string? CaseNumber,
    string Title,
    string Teaser,
    PressReleaseStatus Status,
    bool DraftDiffers,
    DateTime? PublishedAt,
    string? PublishedByName,
    DateTime? DiscordPushedAt,
    DateTime? ModifiedAt);

/// <summary>The one draft the editor is about to show.</summary>
/// <remarks>
/// Id and Status are deliberately absent: the panel already holds the row it asked about, so a copy here would be a
/// spare value — and the record exists to carry the body, which the list row must not.
/// </remarks>
public sealed record PressDraft(string Title, string Teaser, string Html);

/// <summary>Draft input of the settings panel; publishing is a separate call.</summary>
public class PressInput
{
    /// <summary>Null creates a release, otherwise the row to update.</summary>
    public string? Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Teaser { get; set; } = string.Empty;

    /// <summary>Null leaves the stored draft alone, an empty string clears it.</summary>
    /// <remarks>
    /// Without that split a call that only changes the title would wipe the body, and the loss would be silent.
    /// </remarks>
    public string? DraftHtml { get; set; }
}
