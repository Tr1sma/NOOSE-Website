namespace NOOSE_Website.Models.Enums;

/// <summary>Appointment lifecycle status.</summary>
public enum AppointmentStatus
{
    /// <summary>Upcoming, planned.</summary>
    Planned = 0,
    /// <summary>Took place.</summary>
    Perceived = 1,
    /// <summary>Canceled, won't happen.</summary>
    Canceled = 2,
    /// <summary>Postponed, rescheduled.</summary>
    Postponed = 3,
}

/// <summary>Display labels.</summary>
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

    /// <summary>Canceled or postponed.</summary>
    public static bool IsObsolete(AppointmentStatus status) => status is AppointmentStatus.Canceled or AppointmentStatus.Postponed;

    public static readonly IReadOnlyList<AppointmentStatus> All = new[]
    {
        AppointmentStatus.Planned,
        AppointmentStatus.Perceived,
        AppointmentStatus.Canceled,
        AppointmentStatus.Postponed,
    };
}
