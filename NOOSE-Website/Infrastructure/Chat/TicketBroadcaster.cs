namespace NOOSE_Website.Infrastructure.Chat;

/// <summary>Singleton fan-out for the citizen ticket chat; reaches the desk and the citizen's own page alike.</summary>
/// <remarks>
/// Both handles travel because the two sides address a ticket differently: the desk holds the row id, the citizen page
/// only ever knows the case number. Without the second one every citizen circuit would have to reload on every ticket
/// change in the house just to find out whether it was theirs.
/// </remarks>
public sealed class TicketBroadcaster
{
    /// <summary>Fired with the affected ticket's row id and case number when its thread or status changes.</summary>
    public event Action<string, string>? Modified;

    public void Report(string ticketId, string caseNumber) => Modified?.Invoke(ticketId, caseNumber);
}
