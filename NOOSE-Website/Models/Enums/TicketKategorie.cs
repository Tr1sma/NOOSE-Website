namespace NOOSE_Website.Models.Enums;

/// <summary>What a ticket is about; the citizen picks it, the desk may correct it.</summary>
/// <remarks>
/// An enum rather than an admin-managed list, on the precedent of <see cref="TicketArt"/>: a further concern is
/// then one enum value and no migration. <see cref="Sonstiges"/> is the default, so existing rows and every
/// internal ticket land somewhere without a guess.
/// </remarks>
public enum TicketKategorie
{
    Sonstiges = 0,
    Anzeige = 1,
    Auskunft = 2,
    Beschwerde = 3,
    Vermisstenmeldung = 4,
}

/// <summary>Display labels.</summary>
public static class TicketKategorieDisplay
{
    public static string Name(TicketKategorie category) => category switch
    {
        TicketKategorie.Anzeige => "Anzeige",
        TicketKategorie.Auskunft => "Auskunft",
        TicketKategorie.Beschwerde => "Beschwerde",
        TicketKategorie.Vermisstenmeldung => "Vermisstenmeldung",
        TicketKategorie.Sonstiges => "Sonstiges",
        _ => "—",
    };

    /// <summary>One line telling the citizen which concern belongs here.</summary>
    public static string Hint(TicketKategorie category) => category switch
    {
        TicketKategorie.Anzeige => "Sie melden eine Straftat oder einen Verdacht.",
        TicketKategorie.Auskunft => "Sie haben eine Frage an die Behörde.",
        TicketKategorie.Beschwerde => "Sie beschweren sich über einen Einsatz oder eine Entscheidung.",
        TicketKategorie.Vermisstenmeldung => "Sie melden eine vermisste Person.",
        _ => "Alles, was in keine der anderen Kategorien passt.",
    };

    public static readonly IReadOnlyList<TicketKategorie> All = new[]
    {
        TicketKategorie.Anzeige,
        TicketKategorie.Auskunft,
        TicketKategorie.Beschwerde,
        TicketKategorie.Vermisstenmeldung,
        TicketKategorie.Sonstiges,
    };
}
