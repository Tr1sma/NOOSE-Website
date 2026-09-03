using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Infrastructure.Chat;

/// <summary>Process-wide in-memory broadcaster for live tip-thread updates (internal and citizen-facing).</summary>
/// <remarks>
/// Both handles travel because the two sides address a tip differently: the desk holds the row id, the citizen page
/// only ever knows the case number. Without the second one every citizen circuit would have to reload on every tip
/// change in the house just to find out whether it was theirs.
/// <para>
/// The audience travels too. Content never leaks — a citizen reload only ever pulls citizen-facing rows — but the
/// TIMING of every internal note would be signalled into the citizen's circuit, which says when the desk is talking
/// about them. Null means "concerns both threads" (submission, status, deletion). Same shape as
/// <see cref="TicketBroadcaster"/>.
/// </para>
/// </remarks>
public sealed class TipsBroadcaster
{
    /// <summary>Fired with the affected tip's row id, case number and the thread it concerns.</summary>
    public event Action<string, string, TipMessageAudience?>? Modified;

    public void Report(string tipId, string caseNumber, TipMessageAudience? audience = null)
        => Modified?.Invoke(tipId, caseNumber, audience);
}
