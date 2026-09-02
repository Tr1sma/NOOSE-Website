using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Public;

/// <summary>A published warning on the hub. Outward: carries no author and no internal id.</summary>
/// <remarks>
/// The whole body travels on the card, because a warning has no page of its own — it is short, and a detail route
/// would repeat the two fields the card already shows.
/// </remarks>
public sealed record PublicWarningCard(string Title, string Html, DateTime? ValidUntil, DateTime? PublishedAt);

/// <summary>Everything the public warning page reads, cached as one unit.</summary>
/// <param name="SearchText">
/// Bodies as plain text, index-aligned with <paramref name="Cards"/>, computed once per cache fill - see
/// PublicPressSnapshot for the reason. A warning has no detail route, so there is no key to hang them on.
/// </param>
public sealed record PublicWarningSnapshot(
    IReadOnlyList<PublicWarningCard> Cards,
    IReadOnlyList<string>? SearchText = null)
{
    public static PublicWarningSnapshot Empty { get; } = new([]);

    /// <summary>Precomputed plain text of the card at that position; empty when the snapshot carries none.</summary>
    public string SearchTextAt(int index)
        => SearchText is not null && index >= 0 && index < SearchText.Count ? SearchText[index] : string.Empty;
}

/// <summary>Editing row of the settings panel.</summary>
/// <remarks>
/// Carries no HTML on purpose, same reason as PressEdit: a warning holds its pictures as base64 inside the body, so a
/// list row with the draft attached would pull every warning's megabytes to render a table of titles.
/// </remarks>
/// <param name="DraftDiffers">Draft and published copy differ, so publishing would change what visitors read.</param>
/// <param name="IsExpired">Past its expiry, so it is off the public page although its status still says published.</param>
public sealed record WarningEdit(
    string Id,
    string Title,
    PublicWarningStatus Status,
    bool DraftDiffers,
    DateTime? ValidUntil,
    bool IsExpired,
    DateTime? PublishedAt,
    string? PublishedByName,
    DateTime? ModifiedAt);

/// <summary>The one draft the editor is about to show.</summary>
public sealed record WarningDraft(string Title, string Html, DateTime? ValidUntil);

/// <summary>Draft input of the settings panel; publishing is a separate call.</summary>
public class WarningInput
{
    /// <summary>Null creates a warning, otherwise the row to update.</summary>
    public string? Id { get; set; }
    public string Title { get; set; } = string.Empty;

    /// <summary>Null lets the warning stand until it is retracted.</summary>
    public DateTime? ValidUntil { get; set; }

    /// <summary>Null leaves the stored draft alone, an empty string clears it.</summary>
    /// <remarks>
    /// Without that split a call that only changes the title would wipe the body, and the loss would be silent.
    /// </remarks>
    public string? DraftHtml { get; set; }
}
