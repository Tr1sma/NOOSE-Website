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
    /// <param name="attachment">Optional image travelling with the first message.</param>
    Task<string> OpenAsync(TicketInput input, ClaimsPrincipal actor, TicketAttachmentUpload? attachment = null,
        CancellationToken cancellationToken = default);

    // ---- internal ticket (agent) ----

    /// <summary>Opens a ticket for the house; no citizen, no module gate, no quota.</summary>
    Task<string> OpenAsAgentAsync(TicketInput input, IReadOnlyList<string> participantIds, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>Tickets the caller is attached to, with their own unread count over the internal thread.</summary>
    Task<IReadOnlyList<TicketParticipationRow>> GetMyParticipationsAsync(ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    // ---- participants ----

    Task<IReadOnlyList<TicketParticipantRow>> GetParticipantsAsync(string id, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    Task AddParticipantAsync(string id, string agentId, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>Attaches several agents at once; returns how many were newly attached.</summary>
    /// <remarks>
    /// All or nothing on the selectability check: a whole direction goes on the ticket in one write, so a single
    /// unselectable id refuses the batch instead of half-attaching it. Ids already on the ticket are skipped, not
    /// refused — the direction buttons overlap with what is already there by design.
    /// </remarks>
    Task<int> AddParticipantsAsync(string id, IReadOnlyList<string> agentIds, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    Task RemoveParticipantAsync(string participantId, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>The caller's own tickets, newest activity first; empty for an account without a civilian profile.</summary>
    Task<IReadOnlyList<CitizenTicketRow>> GetOwnAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>One of the caller's own tickets; null when it is not theirs.</summary>
    Task<CitizenTicketDetail?> GetOwnDetailAsync(string caseNumber, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>Citizen answer in the shared thread; refused once the ticket is closed.</summary>
    Task ReplyAsCitizenAsync(string caseNumber, string text, ClaimsPrincipal actor,
        TicketAttachmentUpload? attachment = null, CancellationToken cancellationToken = default);

    /// <summary>Moves the citizen's read mark; only the owner may.</summary>
    Task MarkCitizenReadAsync(string caseNumber, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Unread agency messages across the caller's own tickets.</summary>
    Task<int> GetOwnUnreadCountAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    // ---- desk (leadership) ----

    /// <param name="category">Narrows the tab to one concern; null shows every one of them.</param>
    /// <param name="byPriority">Orders by the desk priority instead of the newest activity.</param>
    Task<IReadOnlyList<TicketRow>> GetInboxAsync(TicketInboxScope scope, string? search, bool onlyMine,
        ClaimsPrincipal actor, TicketKategorie? category = null, bool byPriority = false,
        CancellationToken cancellationToken = default);

    /// <summary>Sets or clears the hand-set desk order; null hands the row back to the automatic one.</summary>
    Task SetPriorityAsync(string id, int? priority, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>Corrects what a concern is about; the citizen's pick is a guess, not a classification.</summary>
    Task SetCategoryAsync(string id, TicketKategorie category, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>Tickets for the link picker, latest activity first, across every status.</summary>
    /// <remarks>
    /// Guarded by <c>RequireTicketRead</c> like the inbox, so the picker is the desk's. An agent attached to a single
    /// ticket cannot search here and does not get the type offered; an existing link to their own ticket still
    /// resolves for them, because <c>LinkService</c> asks <c>TicketVisibility</c> per row.
    /// </remarks>
    Task<IReadOnlyList<TicketPickRow>> SearchForLinkAsync(string? term, ClaimsPrincipal actor, int take = 20,
        CancellationToken cancellationToken = default);

    /// <summary>Running tickets for the navigation badge.</summary>
    Task<int> GetOpenCountAsync(CancellationToken cancellationToken = default);

    Task<TicketDetail?> GetAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>One thread of one ticket; the internal one never leaves the house.</summary>
    Task<IReadOnlyList<TicketMessageRow>> GetMessagesAsync(string id, TicketMessageAudience audience,
        ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task AssignSelfAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Puts another agent on the ticket as its handler.</summary>
    /// <remarks>
    /// The handler is one agent and drives the "only mine" filter and the reply bell; participants are a
    /// different axis with its own read marks. Assigning does not attach the handler as a participant.
    /// </remarks>
    Task AssignAsync(string id, string agentId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Moves the status; closing demands a reason, reopening clears it again.</summary>
    /// <param name="reason">Required on the move to closed, ignored on every other move.</param>
    /// <param name="note">Internal remark on the closure; kept out of the audit row and never sent outward.</param>
    Task SetStatusAsync(string id, TicketStatus status, ClaimsPrincipal actor,
        TicketAbschlussgrund? reason = null, string? note = null,
        CancellationToken cancellationToken = default);

    Task PostInternalNoteAsync(string id, string text, ClaimsPrincipal actor,
        TicketAttachmentUpload? attachment = null, CancellationToken cancellationToken = default);

    /// <summary>Agency answer to the citizen; the row carries no agent, so it reads as the constant sender outside.</summary>
    Task ReplyToCitizenAsync(string id, string text, ClaimsPrincipal actor,
        TicketAttachmentUpload? attachment = null, CancellationToken cancellationToken = default);

    /// <summary>What the delivery endpoint needs for one message's attachment, or null when it is not readable.</summary>
    Task<TicketAttachmentAccess?> GetAttachmentAsync(string messageId, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>The same, for the citizen's own thread, addressed by case number and position.</summary>
    Task<TicketAttachmentAccess?> GetOwnAttachmentAsync(string caseNumber, int index, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>Rewrites one line of either thread; its author only, and never a line the citizen wrote.</summary>
    Task EditMessageAsync(string messageId, string text, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>Moves the desk's read mark; a read is not a change to the ticket.</summary>
    Task MarkAgentReadAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    // ---- trash ----

    Task<List<Ticket>> GetTrashAsync(CancellationToken cancellationToken = default);

    Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
