using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Infrastructure.Chat;

/// <summary>Singleton fan-out for the ticket chat; reaches the desk and the citizen's own page alike.</summary>
/// <remarks>
/// Both handles travel because the two sides address a ticket differently: the desk holds the row id, the citizen page
/// only ever knows the case number. Without the second one every citizen circuit would have to reload on every ticket
/// change in the house just to find out whether it was theirs.
/// <para>
/// The audience travels too. Content never leaked — a citizen reload only ever pulls citizen-facing rows — but the
/// TIMING of every internal note was signalled into the citizen's circuit, which says when the desk is talking about
/// them. Null means "concerns both threads" (status, assignment, deletion).
/// </para>
/// </remarks>
public sealed class TicketBroadcaster
{
    /// <summary>Fired with the affected ticket's row id, case number and the thread it concerns.</summary>
    public event Action<string, string, TicketMessageAudience?>? Modified;

    public void Report(string ticketId, string caseNumber, TicketMessageAudience? audience = null)
        => Modified?.Invoke(ticketId, caseNumber, audience);
}
