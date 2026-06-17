using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Appointments;

/// <summary>Form model for creating/editing an appointment.</summary>
public class AppointmentInput
{
    public string Title { get; set; } = string.Empty;
    public AppointmentCategory Category { get; set; } = AppointmentCategory.Misc;
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Planned;
    public string? Location { get; set; }

    /// <summary>Start in local time; service converts to UTC on save.</summary>
    public DateTime? Start { get; set; }

    public DateTime? End { get; set; }

    public bool AllDay { get; set; }
    public string? Description { get; set; }

    public AppointmentVisibilityLevel Visibility { get; set; } = AppointmentVisibilityLevel.Public;
}
