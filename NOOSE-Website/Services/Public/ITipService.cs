using System.Security.Claims;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Services.Public;

/// <summary>Citizen tips: submission, the handler's inbox, and the conversation between the two.</summary>
/// <remarks>
/// Two audiences, one table. Everything a citizen sees comes back as a <c>CitizenTip*</c> record that structurally
/// carries no agent; everything a handler sees comes back as a <c>Tip*</c> record whose citizen fields are blank while
/// the anonymity promise holds.
/// </remarks>
public interface ITipService
{
    // ---- citizen ----

    /// <summary>Files a tip and returns its case number.</summary>
    /// <remarks>The attachment stream is read before the transaction opens; the caller owns and disposes it.</remarks>
    Task<string> SubmitAsync(TipInput input, Stream? attachment, string? contentType, string? originalName,
        ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>The caller's own tips, newest first; empty for an account without a civilian profile.</summary>
    Task<IReadOnlyList<CitizenTipRow>> GetOwnAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>One of the caller's own tips; null when it is not theirs.</summary>
    Task<CitizenTipDetail?> GetOwnDetailAsync(string caseNumber, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>Citizen answer in the shared thread.</summary>
    Task ReplyAsCitizenAsync(string caseNumber, string text, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>Moves the citizen's read mark; only the owner may.</summary>
    Task MarkCitizenReadAsync(string caseNumber, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Unread agency messages across the caller's own tips.</summary>
    Task<int> GetOwnUnreadCountAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    // ---- handler ----

    Task<IReadOnlyList<TipRow>> GetInboxAsync(TipInboxScope scope, string? search, bool onlyMine,
        ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task<TipInboxCounts> GetCountsAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Open tips for the navigation badge.</summary>
    Task<int> GetOpenCountAsync(CancellationToken cancellationToken = default);

    Task<TipDetail?> GetAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Tips of the citizens tied to this person file; the ones under an anonymity promise are left out.</summary>
    Task<IReadOnlyList<TipHistoryRow>> GetForLinkedPersonAsync(string personId, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>The other tips of this one's duplicate group, newest first; empty when it has none.</summary>
    Task<IReadOnlyList<TipDuplicateRow>> GetDuplicatesAsync(string id, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>One thread of one tip; the citizen audience is readable by the owner as well.</summary>
    Task<IReadOnlyList<TipMessageRow>> GetMessagesAsync(string id, TipMessageAudience audience,
        ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task AssignSelfAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task SetStatusAsync(string id, TipStatus status, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task PostInternalNoteAsync(string id, string text, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Agency message to the citizen; the row carries no agent, so it reads as "NOOSE" outside.</summary>
    Task AskCitizenAsync(string id, string text, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Lifts the anonymity promise; leadership only, and it writes its own audit row.</summary>
    Task ResolveAnonymityAsync(string id, string reason, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Attachment access for the delivery endpoint: the owner or a handler, nobody else.</summary>
    Task<TipAttachmentAccess?> GetAttachmentAsync(string id, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    // ---- trash ----

    Task<List<Hinweis>> GetTrashAsync(CancellationToken cancellationToken = default);

    Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
