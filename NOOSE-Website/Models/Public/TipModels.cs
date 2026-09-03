using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Public;

// ---- outward: what the citizen sees of their own tip ----

/// <summary>One of the citizen's own tips in their list.</summary>
/// <remarks>
/// Addressed by case number, never by row id, and it carries no agent at all — the agency answers as "NOOSE", and a
/// record that cannot hold a codename cannot leak one.
/// </remarks>
public record CitizenTipRow(
    string CaseNumber,
    TipStatus Status,
    DateTime CreatedAt,
    string Excerpt,
    string? WantedCaseNumber,
    string? WantedDisplayName,
    bool HasAttachment,
    int UnreadCount);

/// <summary>One of the citizen's own tips, opened.</summary>
public record CitizenTipDetail(
    string CaseNumber,
    TipStatus Status,
    DateTime CreatedAt,
    string Text,
    bool WantsAnonymity,
    bool AnonymityResolved,
    string? WantedCaseNumber,
    string? WantedDisplayName,
    bool HasAttachment,
    string? AttachmentName,
    bool MayReply,
    /// <summary>Kept apart from <paramref name="MayReply"/>, same reason as on a ticket.</summary>
    bool IsBlocked,
    IReadOnlyList<CitizenTipMessage> Messages);

/// <summary>One line of the conversation as the citizen sees it.</summary>
public record CitizenTipMessage(DateTime CreatedAt, string Text, bool FromCitizen);

// ---- inward: the handler's view ----

/// <summary>One tip in the internal inbox.</summary>
/// <remarks>
/// <see cref="TrustTier"/> travels even for an anonymous tip: it is a factor of the visible <see cref="Priority"/>, and
/// the promise covers the identity, not the track record. The exact confirmed count does not — that one is a
/// recognition mark.
/// </remarks>
public record TipRow(
    string Id,
    string CaseNumber,
    TipStatus Status,
    DateTime CreatedAt,
    string Excerpt,
    bool IsAnonymous,
    string? CitizenName,
    string? WantedCaseNumber,
    string? WantedDisplayName,
    bool HasAttachment,
    string? HandlerCodename,
    DateTime? LastMessageAt,
    bool AwaitingAnswer,
    int Priority,
    int TrustTier,
    string? DuplicateGroupId,
    int DuplicateCount);

/// <summary>One tip, opened by a handler.</summary>
/// <remarks>
/// <see cref="CitizenName"/> stays null while anonymity holds — the promise is kept by the projection, not by the page
/// that renders it, so a second page cannot forget it.
/// </remarks>
public record TipDetail(
    string Id,
    string CaseNumber,
    TipStatus Status,
    DateTime CreatedAt,
    string Text,
    bool WantsAnonymity,
    bool AnonymityResolved,
    DateTime? AnonymityResolvedAt,
    string? AnonymityResolvedByCodename,
    string? CitizenName,
    int? CitizenConfirmedTips,
    string? WantedCaseNumber,
    string? WantedDisplayName,
    bool HasAttachment,
    string? AttachmentName,
    string? HandlerId,
    string? HandlerCodename,
    int Priority,
    int? PriorityOverride,
    string? PriorityOverrideReason,
    int TrustTier,
    string? DuplicateGroupId);

/// <summary>One tip of a citizen tied to a person file, as that file's tipster section shows it.</summary>
/// <remarks>
/// The section is keyed on the citizen's identity, so a tip under an anonymity promise never reaches this record —
/// not even as a count, which would name the tipster by arithmetic. <c>TipAnonymity.Disclosable</c> is that filter.
/// </remarks>
public record TipHistoryRow(
    string Id,
    string CaseNumber,
    TipStatus Status,
    DateTime CreatedAt,
    string Excerpt,
    string? CitizenName,
    int TrustTier);

/// <summary>A sibling of the same duplicate group, as a handler sees it.</summary>
public record TipDuplicateRow(string Id, string CaseNumber, TipStatus Status, DateTime CreatedAt, string Excerpt);

/// <summary>A tip that came in against one public notice.</summary>
/// <remarks>
/// Inward, and it carries no citizen field at all — not even where the anonymity promise no longer holds. This is
/// the shape a record-facing reader gets: it answers "what came in about this file", which needs the text and the
/// state and nothing about who wrote it. The audited leadership resolution stays the only way to a name.
/// </remarks>
public record TipNoticeRow(
    string Id, string CaseNumber, TipStatus Status, DateTime CreatedAt, string Excerpt, int Priority);

/// <summary>One line of either thread as a handler sees it.</summary>
public record TipMessageRow(
    string Id,
    TipMessageAudience Audience,
    string Text,
    bool FromCitizen,
    string? AuthorCodename,
    DateTime CreatedAt);

/// <summary>Inbox tab counters.</summary>
public record TipInboxCounts(int New, int InProgress, int Closed);

/// <summary>What the delivery endpoint needs to hand out one attachment.</summary>
public record TipAttachmentAccess(string FileNameSaved, string? ContentType, string? OriginalName);

/// <summary>Form input of the public tip form.</summary>
public class TipInput
{
    public string Text { get; set; } = string.Empty;
    public bool WantsAnonymity { get; set; }

    /// <summary>Public case number of the notice the tip refers to; resolved and verified by the service.</summary>
    public string? WantedCaseNumber { get; set; }
}
