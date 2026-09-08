namespace NOOSE_Website.Models.Enums;

/// <summary>How long an unanswered citizen line has been sitting on the desk.</summary>
/// <remarks>
/// Age, not priority: it says who has been waiting longest, nothing about how important the concern is.
/// </remarks>
public enum TicketReaction
{
    /// <summary>Nobody is waiting: the last line is the agency's, or the ticket is closed.</summary>
    Keine = 0,
    Frisch = 1,
    Faellig = 2,
    Ueberfaellig = 3,
}

/// <summary>Display labels.</summary>
public static class TicketReactionDisplay
{
    public static string Name(TicketReaction reaction) => reaction switch
    {
        TicketReaction.Frisch => "Frisch",
        TicketReaction.Faellig => "Fällig",
        TicketReaction.Ueberfaellig => "Überfällig",
        _ => "—",
    };
}
