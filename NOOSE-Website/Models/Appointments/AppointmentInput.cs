using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Appointments;

/// <summary>Formular-/Eingabemodell zum Anlegen und Bearbeiten eines Termins.</summary>
public class AppointmentInput
{
    public string Title { get; set; } = string.Empty;
    public AppointmentCategory Category { get; set; } = AppointmentCategory.Misc;
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Planned;
    public string? Location { get; set; }

    /// <summary>Beginn (lokale RP-Zeit; der Dienst rechnet beim Speichern in UTC um). Pflicht.</summary>
    public DateTime? Start { get; set; }

    /// <summary>Ende (optional, lokale RP-Zeit).</summary>
    public DateTime? End { get; set; }

    public bool AllDay { get; set; }
    public string? Description { get; set; }

    /// <summary>Sichtbarkeitsstufe: Öffentlich / Eingeschränkt / Privat.</summary>
    public AppointmentVisibilityLevel Visibility { get; set; } = AppointmentVisibilityLevel.Public;
}
