using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Public;

/// <summary>One published organisation, as the outside reads it.</summary>
/// <remarks>
/// Structurally carries no faction id, no case number, no numeric score and no agent identity: what is not on the
/// type cannot be rendered by accident. Both public surfaces — the hub and the hazard ranking — read this one record,
/// because they show the same four facts; a second type would only differ in which of them it forgets.
/// </remarks>
public sealed record PublicFactionCard(
    string DisplayName,
    PublicFactionStanding Standing,
    HazardLevel HazardLevel,
    string? DescriptionHtml,
    DateTime? PublishedAt);

/// <summary>Everything the outside world may read about organisations, in one cached snapshot.</summary>
public sealed record PublicFactionBoard(IReadOnlyList<PublicFactionCard> Cards)
{
    public static PublicFactionBoard Empty { get; } = new([]);
}

/// <summary>Row of the internal management list.</summary>
/// <remarks>
/// Carries no HTML: a description may hold base64 images, and a list of them would be megabytes per render. The editor
/// pulls the one it edits through <c>GetDraftAsync</c>. The publisher is projected to a codename, so the identity user
/// never enters a panel the supervision renders.
/// </remarks>
public sealed record PublicFactionProfileEdit(
    string Id,
    string FactionId,
    string DisplayName,
    string? FactionCaseNumber,
    PublicFactionStanding Standing,
    PublicProfileStatus Status,
    HazardLevel HazardLevel,
    DateTime? PublishedAt,
    string? PublishedByName,
    DateTime? ModifiedAt);

/// <summary>The one profile an author is editing, HTML included.</summary>
public sealed record PublicFactionProfileDraft(
    string Id,
    string FactionId,
    PublicProfileStatus Status,
    string DisplayName,
    PublicFactionStanding Standing,
    string? DescriptionHtml);

/// <summary>What the faction file needs for its banner.</summary>
/// <remarks>No id: the banner states a fact, it does not link into the management list.</remarks>
public sealed record PublicFactionProfileBanner(
    PublicProfileStatus Status,
    PublicFactionStanding Standing,
    DateTime? PublishedAt);

/// <summary>Editable fields of a profile; a class so the panel can two-way bind.</summary>
public class PublicFactionProfileInput
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public PublicFactionStanding Standing { get; set; } = PublicFactionStanding.Beobachtet;

    /// <summary>Null leaves the stored description untouched, "" clears it.</summary>
    public string? DescriptionHtml { get; set; }
}
