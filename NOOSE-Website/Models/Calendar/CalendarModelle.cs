namespace NOOSE_Website.Models.Calendar;

/// <summary>Calendar entry source; controls colour and legend.</summary>
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

/// <summary>Requested calendar view.</summary>
public enum CalendarMode
{
    /// <summary>Personal agenda.</summary>
    My = 0,
    /// <summary>Agency-wide view.</summary>
    Authority = 1,
}

/// <summary>Single calendar entry passed to FullCalendar.</summary>
public record CalendarEntry(
    string Id,
    string Title,
    DateTime StartLocal,
    DateTime? EndLocal,
    bool WholeDay,
    CalendarSource Source,
    string? Href,
    bool Obsolete = false);

/// <summary>Display helper; colour per source.</summary>
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
