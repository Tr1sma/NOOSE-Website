namespace NOOSE_Website.Models.Enums;

/// <summary>Appointment category type.</summary>
public enum AppointmentCategory
{
    /// <summary>Court hearing.</summary>
    CourtDate = 0,
    /// <summary>Internal meeting.</summary>
    Meeting = 1,
    /// <summary>Field deployment.</summary>
    Deployment = 2,
    /// <summary>Deadline or due date.</summary>
    Deadline = 3,
    /// <summary>Other appointment.</summary>
    Misc = 4,
}

/// <summary>Display labels.</summary>
public static class AppointmentCategoryDisplay
{
    public static string Name(AppointmentCategory category) => category switch
    {
        AppointmentCategory.CourtDate => "Gerichtstermin",
        AppointmentCategory.Meeting => "Besprechung",
        AppointmentCategory.Deployment => "Einsatz",
        AppointmentCategory.Deadline => "Frist",
        AppointmentCategory.Misc => "Sonstiges",
        _ => "—",
    };

    public static readonly IReadOnlyList<AppointmentCategory> All = new[]
    {
        AppointmentCategory.CourtDate,
        AppointmentCategory.Meeting,
        AppointmentCategory.Deployment,
        AppointmentCategory.Deadline,
        AppointmentCategory.Misc,
    };
}
