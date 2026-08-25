namespace NOOSE_Website.Models.Enums;

/// <summary>Handling state of a citizen ticket.</summary>
public enum TicketStatus
{
    Offen = 0,
    InBearbeitung = 1,
    WartetAufBuerger = 2,
    Geschlossen = 3,
}

/// <summary>Display labels.</summary>
public static class TicketStatusDisplay
{
    public static string Name(TicketStatus status) => status switch
    {
        TicketStatus.Offen => "Offen",
        TicketStatus.InBearbeitung => "In Bearbeitung",
        TicketStatus.WartetAufBuerger => "Wartet auf Bürger",
        TicketStatus.Geschlossen => "Geschlossen",
        _ => "—",
    };

    /// <summary>What the citizen is told; "waiting for you" is a request, not a state of the desk.</summary>
    public static string CitizenName(TicketStatus status) => status switch
    {
        TicketStatus.Offen => "Eingegangen",
        TicketStatus.InBearbeitung => "In Bearbeitung",
        TicketStatus.WartetAufBuerger => "Antwort erwartet",
        TicketStatus.Geschlossen => "Abgeschlossen",
        _ => "—",
    };

    public static readonly IReadOnlyList<TicketStatus> All = new[]
    {
        TicketStatus.Offen,
        TicketStatus.InBearbeitung,
        TicketStatus.WartetAufBuerger,
        TicketStatus.Geschlossen,
    };
}
