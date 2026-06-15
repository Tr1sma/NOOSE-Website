namespace NOOSE_Website.Models.Calendar;

/// <summary>
/// Quelle/Herkunft eines Kalendereintrags – steuert Farbe und Legende. <see cref="Termin"/> ist die eigene
/// Termin-Akte; die übrigen werden aus bestehenden datierten Akten aggregiert (rein lesend).
/// </summary>
public enum CalendarSource
{
    Appointment = 0,
    Operation = 1,
    Observation = 2,
    Job = 3,
    Followup = 4,
    FactionActivity = 5,
    PersonDoc = 6,
}

/// <summary>Welche Kalender-Sicht angefragt wird.</summary>
public enum CalendarMode
{
    /// <summary>Persönliche Agenda: eigene Termine + zugewiesene Aufgaben + eigene Wiedervorlagen.</summary>
    My = 0,
    /// <summary>Behörden-weit (für alle): öffentliche Termine + operative Akten inkl. Personen-Doks.</summary>
    Authority = 1,
}

/// <summary>
/// Ein einzelner Kalendereintrag (wird an FullCalendar übergeben). <see cref="Id"/> ist global eindeutig
/// (quellen-präfixiert, z. B. „tm:…"/„op:…"). Zeiten sind LOKALE Wandzeit (RP-Zeit) – FullCalendar rendert mit
/// timeZone:'local'. <see cref="EndeLokal"/> = null bedeutet punktförmig/offenes Ende. <see cref="Hinfaellig"/>
/// (abgesagt/verschoben/abgebrochen) wird gedämpft dargestellt.
/// </summary>
public record CalendarEntry(
    string Id,
    string Title,
    DateTime StartLocal,
    DateTime? EndLocal,
    bool WholeDay,
    CalendarSource Source,
    string? Href,
    bool Obsolete = false);

/// <summary>Anzeige-Helfer (Farbe + Name je Quelle); Farben konsistent zur graph.js-Palette.</summary>
public static class CalendarDisplay
{
    public static string Colour(CalendarSource source) => source switch
    {
        CalendarSource.Appointment => "#3FB950",
        CalendarSource.Operation => "#F0883E",
        CalendarSource.Observation => "#58A6FF",
        CalendarSource.Job => "#8B98A8",
        CalendarSource.Followup => "#D29922",
        CalendarSource.FactionActivity => "#7C8CF8",
        CalendarSource.PersonDoc => "#A371F7",
        _ => "#8B98A8",
    };

    public static string Name(CalendarSource source) => source switch
    {
        CalendarSource.Appointment => "Termine",
        CalendarSource.Operation => "Operationen",
        CalendarSource.Observation => "Observationen",
        CalendarSource.Job => "Aufgaben (fällig)",
        CalendarSource.Followup => "Wiedervorlagen",
        CalendarSource.FactionActivity => "Fraktions-Aktivitäten",
        CalendarSource.PersonDoc => "Personen-Doks",
        _ => "—",
    };

    public static readonly IReadOnlyList<CalendarSource> All = new[]
    {
        CalendarSource.Appointment,
        CalendarSource.Operation,
        CalendarSource.Observation,
        CalendarSource.Job,
        CalendarSource.Followup,
        CalendarSource.FactionActivity,
        CalendarSource.PersonDoc,
    };
}
