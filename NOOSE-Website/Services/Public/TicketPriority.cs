using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services.Public;

/// <summary>Order of the desk: what the concern is, how long it has waited, and who is asking.</summary>
/// <remarks>
/// Three bands with a floor of 1 each, multiplied, on the pattern of <see cref="TipPriority"/>: every factor may
/// raise the order, none may erase it. A brand-new question from a first-time account still scores above zero.
/// <para>
/// Unlike a tip's priority this is never stored as an automatic value. It ages by the hour, so a stamped number
/// would be wrong within the day and would need a sweep to stay right; the desk reads at most
/// <c>ListCap</c> rows and computes it there. Only a hand-set priority is stored, because that one is a decision.
/// </para>
/// </remarks>
public static class TicketPriority
{
    public const int Min = 1;

    public const int Max = 5 * 5 * TipTrust.MaxTier;

    /// <summary>What the concern is, as a band 1..5.</summary>
    /// <remarks>
    /// A missing person outranks a complaint, and "Sonstiges" is not the bottom: an unsorted concern may be
    /// anything, and burying it under every filed one is how it goes unread.
    /// </remarks>
    public static int CategoryBand(TicketKategorie category) => category switch
    {
        TicketKategorie.Vermisstenmeldung => 5,
        TicketKategorie.Anzeige => 4,
        TicketKategorie.Beschwerde => 3,
        _ => 2,
    };

    /// <summary>How long the newest citizen line has gone unanswered, as a band 1..5.</summary>
    public static int WaitBand(TimeSpan? waited)
    {
        if (waited is not { } age || age <= TimeSpan.Zero)
        {
            return 1;
        }
        if (age >= TicketRules.ReactionOverdue * 3)
        {
            return 5;
        }
        if (age >= TicketRules.ReactionOverdue)
        {
            return 4;
        }
        return age >= TicketRules.ReactionDue ? 3 : 2;
    }

    /// <summary>The automatic order of one ticket.</summary>
    /// <param name="waited">Time the newest citizen line has been unanswered; null when nobody is waiting.</param>
    /// <param name="confirmedTips">Track record of the account behind the ticket; 0 for an internal one.</param>
    public static int Compute(TicketKategorie category, TimeSpan? waited, int confirmedTips)
        => CategoryBand(category) * WaitBand(waited) * TipTrust.Tier(confirmedTips);

    /// <summary>Keeps a hand-set priority inside the range the automatic one can reach.</summary>
    public static int Clamp(int priority) => Math.Clamp(priority, Min, Max);
}
