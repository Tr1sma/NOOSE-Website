namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Kategorie eines Termins – Phase 8 (Block C). Steuert Anzeige (Chip/Farbe) im Kalender und auf der
/// Detailseite sowie den Filter. Default ist <see cref="Sonstiges"/>.
/// </summary>
public enum AppointmentCategory
{
    /// <summary>Gerichtstermin/Verhandlung.</summary>
    CourtDate = 0,
    /// <summary>Interne Besprechung/Meeting.</summary>
    Meeting = 1,
    /// <summary>Geplanter Einsatz/Termin im Feld.</summary>
    Deployment = 2,
    /// <summary>Frist/Deadline.</summary>
    Deadline = 3,
    /// <summary>Sonstiger Termin.</summary>
    Misc = 4,
}

/// <summary>Anzeigetexte für die Termin-Kategorie (UI-frei, ohne MudBlazor-Abhängigkeit).</summary>
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
