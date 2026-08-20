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
    IReadOnlyList<CitizenTicketMessage> Messages);

/// <summary>One line of the conversation as the citizen sees it.</summary>
/// <remarks>No author field at all: the sender is either the citizen or the constant agency name.</remarks>
public record CitizenTicketMessage(DateTime CreatedAt, string Text, bool FromCitizen);

// ---- inward: the desk's view ----

/// <summary>One ticket in the leadership desk.</summary>
public record TicketRow(
    string Id,
    string CaseNumber,
    string Subject,
    TicketStatus Status,
    DateTime CreatedAt,
    DateTime LastActivityAt,
    string CitizenName,
    string? HandlerCodename,
    bool AwaitingAnswer,
    int UnreadCount);

/// <summary>One ticket, opened by a handler.</summary>
public record TicketDetail(
    string Id,
    string CaseNumber,
    string Subject,
    TicketStatus Status,
    DateTime CreatedAt,
    DateTime LastActivityAt,
    string CitizenName,
    bool CitizenIsBlocked,
    string? HandlerId,
    string? HandlerCodename,
    DateTime? ClosedAt,
    string? ClosedByCodename);

/// <summary>One line of either thread as a handler sees it.</summary>
public record TicketMessageRow(
    string Id,
    TicketMessageAudience Audience,
    string Text,
    bool FromCitizen,
    string? AuthorCodename,
    DateTime CreatedAt);

/// <summary>Desk tab counters.</summary>
public record TicketInboxCounts(int Open, int InProgress, int Waiting, int Closed);

/// <summary>Form input when a citizen opens a ticket.</summary>
public class TicketInput
{
    public string Subject { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}
