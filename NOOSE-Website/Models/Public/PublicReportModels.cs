using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Public;

/// <summary>A published report on the hub. Outward: carries no author, no anchor and no internal id.</summary>
/// <remarks>
/// Structurally unable to carry a field of the internal snapshot — no SituationReportId, no SnapshotJson, no codename.
/// That is the first of the two layers keeping the frozen statistics inside; the second is a behavioural test.
/// </remarks>
public sealed record PublicReportCard(int Year, int Month, string Title, DateTime? PublishedAt);

/// <summary>A published report in full.</summary>
public sealed record PublicReportView(int Year, int Month, string Title, string Html, DateTime? PublishedAt);

/// <summary>Everything the public report pages read, cached as one unit.</summary>
public sealed record PublicReportSnapshot(
    IReadOnlyList<PublicReportCard> Cards,
    IReadOnlyDictionary<string, PublicReportView> ByPeriod)
{
    public static PublicReportSnapshot Empty { get; } =
        new([], new Dictionary<string, PublicReportView>(StringComparer.OrdinalIgnoreCase));

    public PublicReportView? Find(string? period)
        => period is not null && ByPeriod.TryGetValue(period, out var view) ? view : null;
}

/// <summary>Editing row of the settings panel.</summary>
/// <remarks>
/// Carries no HTML on purpose, same reason as PressEdit: a report holds its pictures as base64 inside the body, so a
/// list row with the draft attached would pull every report's megabytes to render a table of titles.
/// </remarks>
/// <param name="DraftDiffers">Draft and published copy differ, so publishing would change what visitors read.</param>
/// <param name="HasAnchor">The archived monthly report still exists; the panel links to it while it does.</param>
public sealed record PublicReportEdit(
    string Id,
    int Year,
    int Month,
    string Title,
    PublicReportStatus Status,
    bool DraftDiffers,
    DateTime? PublishedAt,
    string? PublishedByName,
    string? SituationReportId,
    bool HasAnchor,
    DateTime? ModifiedAt);

/// <summary>The one draft the editor is about to show.</summary>
public sealed record PublicReportDraft(string Title, string Html);

/// <summary>An archived monthly report that has no living public text yet.</summary>
/// <remarks>
/// Projected down to what the picker needs: SituationReportHead also names the agent who generated the report, and
/// that has no business in a list the panel renders.
/// </remarks>
public sealed record PublicReportAnchor(string Id, int Year, int Month, string Title);

/// <summary>Draft input of the settings panel; publishing is a separate call.</summary>
public class PublicReportInput
{
    /// <summary>Null creates a report, otherwise the row to update.</summary>
    public string? Id { get; set; }

    /// <summary>The archived monthly report to anchor a new row on; ignored when editing.</summary>
    /// <remarks>
    /// Year and month are taken from the anchor, never from the caller — otherwise the public address would be
    /// forgeable.
    /// </remarks>
    public string? SituationReportId { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>Null leaves the stored draft alone, an empty string clears it.</summary>
    /// <remarks>
    /// Without that split a call that only changes the title would wipe the body, and the loss would be silent.
    /// </remarks>
    public string? DraftHtml { get; set; }
}
