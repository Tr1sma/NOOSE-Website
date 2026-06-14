namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Lebenszyklus-Status eines Termins – Phase 8 (Block C). Default <see cref="Geplant"/>; abgesagte/verschobene
/// Termine werden im Kalender gedämpft (durchgestrichen) dargestellt.
/// </summary>
public enum AppointmentStatus
{
    /// <summary>Geplant, steht noch an.</summary>
    Planned = 0,
    /// <summary>Wahrgenommen/stattgefunden.</summary>
    Perceived = 1,
    /// <summary>Abgesagt (findet nicht statt).</summary>
    Canceled = 2,
    /// <summary>Verschoben (neuer Termin folgt separat).</summary>
    Postponed = 3,
}

/// <summary>Anzeigetexte für den Termin-Status (UI-frei, ohne MudBlazor-Abhängigkeit).</summary>
public static class AppointmentStatusDisplay
{
    public static string Name(AppointmentStatus status) => status switch
    {
        AppointmentStatus.Planned => "Geplant",
        AppointmentStatus.Perceived => "Wahrgenommen",
        AppointmentStatus.Canceled => "Abgesagt",
        AppointmentStatus.Postponed => "Verschoben",
        _ => "—",
    };

    /// <summary>Abgesagt oder verschoben – im Kalender gedämpft darstellen.</summary>
    public static bool IsObsolete(AppointmentStatus status) => status is AppointmentStatus.Canceled or AppointmentStatus.Postponed;

    public static readonly IReadOnlyList<AppointmentStatus> All = new[]
    {
        AppointmentStatus.Planned,
        AppointmentStatus.Perceived,
        AppointmentStatus.Canceled,
        AppointmentStatus.Postponed,
    };
}
