namespace NOOSE_Website.Models.Enums;

/// <summary>Why a ticket was closed; required on the move to closed, cleared again on reopening.</summary>
/// <remarks>
/// Internal only. The citizen is told "Abgeschlossen" and nothing more — a reason is the desk's own record of what
/// happened, and "Spam" is not a sentence anyone should read about their own concern.
/// </remarks>
public enum TicketAbschlussgrund
{
    Erledigt = 0,
    KeinAnliegen = 1,
    Spam = 2,
    KeinKontakt = 3,
    Weitergeleitet = 4,
}

/// <summary>Display labels.</summary>
public static class TicketAbschlussgrundDisplay
{
    public static string Name(TicketAbschlussgrund reason) => reason switch
    {
        TicketAbschlussgrund.Erledigt => "Erledigt",
        TicketAbschlussgrund.KeinAnliegen => "Kein Anliegen",
        TicketAbschlussgrund.Spam => "Spam",
        TicketAbschlussgrund.KeinKontakt => "Kein Kontakt",
        TicketAbschlussgrund.Weitergeleitet => "Weitergeleitet",
        _ => "—",
    };

    public static readonly IReadOnlyList<TicketAbschlussgrund> All = new[]
    {
        TicketAbschlussgrund.Erledigt,
        TicketAbschlussgrund.KeinAnliegen,
        TicketAbschlussgrund.Spam,
        TicketAbschlussgrund.KeinKontakt,
        TicketAbschlussgrund.Weitergeleitet,
    };
}
