using System.Security.Claims;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Services.Public;

/// <summary>Citizen tickets to leadership: opening, the desk, and the conversation between the two.</summary>
/// <remarks>
/// Two audiences, one table. A citizen addresses a ticket by its case number and gets back a <c>CitizenTicket*</c>
/// record that structurally carries no agent; the desk addresses it by row id. That split is the same one
/// <see cref="ITipService"/> draws, and for the same reason: a raw row id from outside would be an existence oracle.
/// </remarks>
public interface ITicketService
{
    // ---- citizen ----

    /// <summary>Opens a ticket with its first message and returns the case number.</summary>
    Task<string> OpenAsync(TicketInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>The caller's own tickets, newest activity first; empty for an account without a civilian profile.</summary>
    Task<IReadOnlyList<CitizenTicketRow>> GetOwnAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>One of the caller's own tickets; null when it is not theirs.</summary>
    Task<CitizenTicketDetail?> GetOwnDetailAsync(string caseNumber, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>Citizen answer in the shared thread; refused once the ticket is closed.</summary>
    Task ReplyAsCitizenAsync(string caseNumber, string text, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>Moves the citizen's read mark; only the owner may.</summary>
    Task MarkCitizenReadAsync(string caseNumber, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Unread agency messages across the caller's own tickets.</summary>
    Task<int> GetOwnUnreadCountAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    // ---- desk (leadership) ----

    Task<IReadOnlyList<TicketRow>> GetInboxAsync(TicketInboxScope scope, string? search, bool onlyMine,
        ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Running tickets for the navigation badge.</summary>
    Task<int> GetOpenCountAsync(CancellationToken cancellationToken = default);

    Task<TicketDetail?> GetAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>One thread of one ticket; the internal one never leaves the house.</summary>
    Task<IReadOnlyList<TicketMessageRow>> GetMessagesAsync(string id, TicketMessageAudience audience,
        ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task AssignSelfAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task SetStatusAsync(string id, TicketStatus status, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    Task PostInternalNoteAsync(string id, string text, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>Agency answer to the citizen; the row carries no agent, so it reads as the constant sender outside.</summary>
    Task ReplyToCitizenAsync(string id, string text, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>Moves the desk's read mark; a read is not a change to the ticket.</summary>
    Task MarkAgentReadAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    // ---- trash ----

    Task<List<Ticket>> GetTrashAsync(CancellationToken cancellationToken = default);

    Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
