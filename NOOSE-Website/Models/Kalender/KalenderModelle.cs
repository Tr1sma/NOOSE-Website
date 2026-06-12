namespace NOOSE_Website.Models.Kalender;

/// <summary>
/// Quelle/Herkunft eines Kalendereintrags – steuert Farbe und Legende. <see cref="Termin"/> ist die eigene
/// Termin-Akte; die übrigen werden aus bestehenden datierten Akten aggregiert (rein lesend).
/// </summary>
public enum KalenderQuelle
{
    Termin = 0,
    Operation = 1,
    Observation = 2,
    Aufgabe = 3,
    Wiedervorlage = 4,
    FraktionAktivitaet = 5,
    PersonDok = 6,
}

/// <summary>Welche Kalender-Sicht angefragt wird.</summary>
public enum KalenderModus
{
    /// <summary>Persönliche Agenda: eigene Termine + zugewiesene Aufgaben + eigene Wiedervorlagen.</summary>
    Mein = 0,
    /// <summary>Behörden-weit (für alle): öffentliche Termine + operative Akten inkl. Personen-Doks.</summary>
    Behoerde = 1,
}

/// <summary>
/// Ein einzelner Kalendereintrag (wird an FullCalendar übergeben). <see cref="Id"/> ist global eindeutig
/// (quellen-präfixiert, z. B. „tm:…"/„op:…"). Zeiten sind LOKALE Wandzeit (RP-Zeit) – FullCalendar rendert mit
/// timeZone:'local'. <see cref="EndeLokal"/> = null bedeutet punktförmig/offenes Ende. <see cref="Hinfaellig"/>
/// (abgesagt/verschoben/abgebrochen) wird gedämpft dargestellt.
/// </summary>
public record KalenderEintrag(
    string Id,
    string Titel,
    DateTime StartLokal,
    DateTime? EndeLokal,
    bool GanzTaegig,
    KalenderQuelle Quelle,
    string? Href,
    bool Hinfaellig = false);

/// <summary>Anzeige-Helfer (Farbe + Name je Quelle); Farben konsistent zur graph.js-Palette.</summary>
public static class KalenderAnzeige
{
    public static string Farbe(KalenderQuelle quelle) => quelle switch
    {
        KalenderQuelle.Termin => "#3FB950",
        KalenderQuelle.Operation => "#F0883E",
        KalenderQuelle.Observation => "#58A6FF",
        KalenderQuelle.Aufgabe => "#8B98A8",
        KalenderQuelle.Wiedervorlage => "#D29922",
        KalenderQuelle.FraktionAktivitaet => "#7C8CF8",
        KalenderQuelle.PersonDok => "#A371F7",
        _ => "#8B98A8",
    };

    public static string Name(KalenderQuelle quelle) => quelle switch
    {
        KalenderQuelle.Termin => "Termine",
        KalenderQuelle.Operation => "Operationen",
        KalenderQuelle.Observation => "Observationen",
        KalenderQuelle.Aufgabe => "Aufgaben (fällig)",
        KalenderQuelle.Wiedervorlage => "Wiedervorlagen",
        KalenderQuelle.FraktionAktivitaet => "Fraktions-Aktivitäten",
        KalenderQuelle.PersonDok => "Personen-Doks",
        _ => "—",
    };

    public static readonly IReadOnlyList<KalenderQuelle> Alle = new[]
    {
        KalenderQuelle.Termin,
        KalenderQuelle.Operation,
        KalenderQuelle.Observation,
        KalenderQuelle.Aufgabe,
        KalenderQuelle.Wiedervorlage,
        KalenderQuelle.FraktionAktivitaet,
        KalenderQuelle.PersonDok,
    };
}
