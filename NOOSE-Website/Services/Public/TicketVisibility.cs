using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;

namespace NOOSE_Website.Services.Public;

/// <summary>Who may open one ticket and who may read its internal thread; query and point check in one place.</summary>
/// <remarks>
/// Modelled on <c>TaskforceVisibility</c>. Two different questions on purpose:
/// <list type="bullet">
/// <item>the desk (leadership) sees every ticket and every internal note — it is their correspondence;</item>
/// <item>an agent attached to a single ticket sees that one, and nothing else.</item>
/// </list>
/// The read-only supervision reads along, like everywhere else in the house; a ticket is not a classified record.
/// </remarks>
public static class TicketVisibility
{
    /// <summary>True for the desk itself: leadership and the read-only supervision above it.</summary>
    public static bool IsDesk(ClaimsPrincipal actor) => actor.MayClassifiedRead();

    /// <summary>True when the actor is attached to this ticket.</summary>
    public static async Task<bool> IsParticipantAsync(AppDbContext db, string ticketId, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        var agentId = actor.GetAgentId();
        if (string.IsNullOrEmpty(agentId))
        {
            return false;
        }
        return await db.TicketBeteiligte.AsNoTracking()
            .AnyAsync(p => p.TicketId == ticketId && p.AgentId == agentId, cancellationToken);
    }

    /// <summary>True when the actor may open this one ticket at all.</summary>
    public static async Task<bool> MayReadAsync(AppDbContext db, string ticketId, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
        => IsDesk(actor) || await IsParticipantAsync(db, ticketId, actor, cancellationToken);

    /// <summary>True when the actor may read and write the internal thread of this ticket.</summary>
    /// <remarks>Same set as <see cref="MayReadAsync"/> today; separate, because the citizen thread may narrow later.</remarks>
    public static Task<bool> MayReadInternalAsync(AppDbContext db, string ticketId, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
        => MayReadAsync(db, ticketId, actor, cancellationToken);
}
