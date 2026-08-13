using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Public;

/// <summary>One card on the public board.</summary>
/// <remarks>
/// Structurally carries no record id, no agent identity and no numeric score: what is not on the type cannot be
/// rendered by accident. <c>PublicWantedModelTests</c> holds that shape.
/// </remarks>
public sealed record PublicWantedCard(
    string CaseNumber,
    PublicWantedKind Kind,
    string DisplayName,
    string? AliasText,
    bool HasPhoto,
    HazardLevel HazardLevel,
    DateTime? PublishedAt);

/// <summary>A public wanted profile.</summary>
public sealed record PublicWantedDetail(
    string CaseNumber,
    PublicWantedKind Kind,
    string DisplayName,
    string? AliasText,
    bool HasPhoto,
    HazardLevel HazardLevel,
    DateTime? PublishedAt,
    string? ChargeHtml,
    string? LastArea,
    string? VehicleText,
    DateTime? ExpiresAt);

/// <summary>Everything the outside world may read, in one cached snapshot.</summary>
public sealed record PublicWantedBoard(
    IReadOnlyList<PublicWantedCard> Cards,
    IReadOnlyDictionary<string, PublicWantedDetail> ByCaseNumber)
{
    public static PublicWantedBoard Empty { get; } =
        new([], new Dictionary<string, PublicWantedDetail>(StringComparer.OrdinalIgnoreCase));

    public PublicWantedDetail? Find(string? caseNumber)
        => caseNumber is not null && ByCaseNumber.TryGetValue(caseNumber, out var entry) ? entry : null;
}

/// <summary>What the anonymous photo endpoint needs to stream a file, and nothing else.</summary>
public sealed record PublicWantedPhoto(string FileNameSaved, string ContentType);

/// <summary>Row of the internal management list.</summary>
/// <remarks>
/// Carries no HTML: an accusation may hold base64 images, and a list of them would be megabytes per render. The
/// editor pulls the one it edits through <c>GetDraftAsync</c>.
/// </remarks>
public sealed record PublicWantedEdit(
    string Id,
    string? CaseNumber,
    PublicWantedKind Kind,
    PublicWantedStatus Status,
    string DisplayName,
    string? PersonCaseNumber,
    HazardLevel HazardLevel,
    bool HasPhoto,
    DateTime? PublishedAt,
    string? PublishedByName,
    DateTime? ExpiresAt,
    DateTime? ModifiedAt);

/// <summary>The one notice an author is editing, HTML included.</summary>
public sealed record PublicWantedDraft(
    string Id,
    string? CaseNumber,
    PublicWantedStatus Status,
    string DisplayName,
    string? AliasText,
    string? LastArea,
    string? VehicleText,
    string? PhotoSourceId,
    DateTime? ExpiresAt,
    string? ChargeHtml);

/// <summary>One selectable file photo, labelled by upload date rather than by file name.</summary>
public sealed record PublicWantedPhotoOption(string Id, string Label);

/// <summary>What the editor may offer: the file's photos and its recorded areas.</summary>
public sealed record PublicWantedOptions(
    IReadOnlyList<PublicWantedPhotoOption> Photos,
    IReadOnlyList<string> Areas)
{
    public static PublicWantedOptions Empty { get; } = new([], []);
}

/// <summary>What the file page needs for its banner.</summary>
/// <remarks>No id: the banner states a fact, it does not link into the management list, and it renders for partners.</remarks>
public sealed record PublicWantedBanner(
    string? CaseNumber,
    PublicWantedStatus Status,
    DateTime? PublishedAt);

/// <summary>What publishing did: went live, or turned into a request for leadership.</summary>
public enum PublicWantedPublishOutcome
{
    Published = 0,
    Requested = 1,
}

/// <summary>One pending publication request in the approval inbox.</summary>
public sealed record PublicWantedRequestRow(
    string RequestId,
    string WantedId,
    string DisplayName,
    string TargetDesignation,
    string? RequesterName,
    string? Justification,
    DateTime CreatedAt);

/// <summary>Editable fields of a notice; a class so the panel can two-way bind.</summary>
public class PublicWantedInput
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AliasText { get; set; }
    public string? LastArea { get; set; }
    public string? VehicleText { get; set; }
    public string? PhotoSourceId { get; set; }
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Null leaves the stored accusation untouched, "" clears it.</summary>
    public string? ChargeHtml { get; set; }
}
