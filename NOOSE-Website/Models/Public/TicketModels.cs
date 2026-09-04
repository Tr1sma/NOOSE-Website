using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Public;

// ---- outward: what the citizen sees of their own ticket ----

/// <summary>One of the citizen's own tickets in their list.</summary>
/// <remarks>
/// Addressed by case number, never by row id, and it carries no agent at all — the agency answers under a constant
/// sender, and a record that cannot hold a codename cannot leak one.
/// </remarks>
public record CitizenTicketRow(
    string CaseNumber,
    string Subject,
    TicketStatus Status,
    DateTime CreatedAt,
    DateTime LastActivityAt,
    int UnreadCount);

/// <summary>One of the citizen's own tickets, opened.</summary>
public record CitizenTicketDetail(
    string CaseNumber,
    string Subject,
    TicketStatus Status,
    DateTime CreatedAt,
    bool MayReply,
    /// <summary>Kept apart from <paramref name="MayReply"/>: a blocked account must not be told its open ticket
    /// is closed, which is what one collapsed flag said.</summary>
    bool IsBlocked,
    IReadOnlyList<CitizenTicketMessage> Messages);

/// <summary>One line of the conversation as the citizen sees it.</summary>
/// <remarks>
/// No author field at all: the sender is either the citizen or the constant agency name. <paramref name="EditedAt"/>
/// is the one thing a rewritten agency line owes the reader — a text they may already have read can change, and
/// saying so costs no identity.
/// </remarks>
public record CitizenTicketMessage(DateTime CreatedAt, string Text, bool FromCitizen, DateTime? EditedAt);

// ---- inward: the desk's view ----

/// <summary>One ticket in the leadership desk.</summary>
public record TicketRow(
    string Id,
    string CaseNumber,
    string Subject,
    TicketStatus Status,
    TicketArt Kind,
    DateTime CreatedAt,
    DateTime LastActivityAt,
    string CitizenName,
    string? HandlerCodename,
    bool AwaitingAnswer,
    int UnreadCount);

/// <summary>One ticket, opened by a handler.</summary>
/// <param name="CitizenName">Empty for an internal ticket: there is no citizen behind it.</param>
public record TicketDetail(
    string Id,
    string CaseNumber,
    string Subject,
    TicketStatus Status,
    TicketArt Kind,
    DateTime CreatedAt,
    DateTime LastActivityAt,
    string CitizenName,
    bool CitizenIsBlocked,
    string? HandlerId,
    string? HandlerCodename,
    string? OpenedByCodename,
    DateTime? ClosedAt,
    string? ClosedByCodename);

/// <summary>One agent attached to a ticket, as the desk lists them.</summary>
public record TicketParticipantRow(string Id, string AgentId, string Codename, string? RealName, DateTime AddedAt);

/// <summary>One line of either thread as a handler sees it.</summary>
/// <remarks>
/// <paramref name="Mine"/> instead of an author id: the citizen-facing row carries no agent by design, so ownership
/// lives in the audit stamp. The service compares it, which keeps an account id out of the record entirely.
/// </remarks>
public record TicketMessageRow(
    string Id,
    TicketMessageAudience Audience,
    string Text,
    bool FromCitizen,
    string? AuthorCodename,
    DateTime CreatedAt,
    DateTime? EditedAt,
    bool Mine);

/// <summary>Form input when a citizen opens a ticket.</summary>
public class TicketInput
{
    public string Subject { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

/// <summary>One internal ticket in the list of the agent attached to it.</summary>
public record TicketParticipationRow(
    string Id,
    string CaseNumber,
    string Subject,
    TicketStatus Status,
    TicketArt Kind,
    DateTime LastActivityAt,
    int UnreadInternal);

/// <summary>One ticket in the link picker.</summary>
/// <remarks>The subject is here so the desk can pick the right row; it never travels into the link, where a
/// ticket shows its case number and nothing else.</remarks>
public record TicketPickRow(
    string Id,
    string CaseNumber,
    string Subject,
    TicketStatus Status,
    DateTime LastActivityAt);
