namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Kategorie eines Termins – Phase 8 (Block C). Steuert Anzeige (Chip/Farbe) im Kalender und auf der
/// Detailseite sowie den Filter. Default ist <see cref="Sonstiges"/>.
/// </summary>
public enum TerminKategorie
{
    /// <summary>Gerichtstermin/Verhandlung.</summary>
    Gerichtstermin = 0,
    /// <summary>Interne Besprechung/Meeting.</summary>
    Besprechung = 1,
    /// <summary>Geplanter Einsatz/Termin im Feld.</summary>
    Einsatz = 2,
    /// <summary>Frist/Deadline.</summary>
    Frist = 3,
    /// <summary>Sonstiger Termin.</summary>
    Sonstiges = 4,
}

/// <summary>Anzeigetexte für die Termin-Kategorie (UI-frei, ohne MudBlazor-Abhängigkeit).</summary>
public static class TerminKategorieAnzeige
{
    public static string Name(TerminKategorie kategorie) => kategorie switch
    {
        TerminKategorie.Gerichtstermin => "Gerichtstermin",
        TerminKategorie.Besprechung => "Besprechung",
        TerminKategorie.Einsatz => "Einsatz",
        TerminKategorie.Frist => "Frist",
        TerminKategorie.Sonstiges => "Sonstiges",
        _ => "—",
    };

    public static readonly IReadOnlyList<TerminKategorie> Alle = new[]
    {
        TerminKategorie.Gerichtstermin,
        TerminKategorie.Besprechung,
        TerminKategorie.Einsatz,
        TerminKategorie.Frist,
        TerminKategorie.Sonstiges,
    };
}
