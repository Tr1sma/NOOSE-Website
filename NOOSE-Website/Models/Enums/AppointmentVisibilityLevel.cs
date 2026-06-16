using MudBlazor;

namespace NOOSE_Website.Models.Enums;

/// <summary>Appointment visibility level.</summary>
public enum AppointmentVisibilityLevel
{
    /// <summary>Visible to all agents.</summary>
    Public = 0,
    /// <summary>Creator, attendees, leadership.</summary>
    Restricted = 1,
    /// <summary>Creator and leadership only.</summary>
    Private = 2,
}

/// <summary>Display labels.</summary>
public static class AppointmentVisibilityLevelDisplay
{
    public static string Name(AppointmentVisibilityLevel level) => level switch
    {
        AppointmentVisibilityLevel.Public => "Öffentlich",
        AppointmentVisibilityLevel.Restricted => "Eingeschränkt",
        AppointmentVisibilityLevel.Private => "Privat",
        _ => "—",
    };

    public static string Help(AppointmentVisibilityLevel level) => level switch
    {
        AppointmentVisibilityLevel.Public => "Im Behörden-Kalender für alle aktiven Agenten sichtbar.",
        AppointmentVisibilityLevel.Restricted => "Nur Ersteller, zugeteilte Teilnehmer und die Leitung.",
        AppointmentVisibilityLevel.Private => "Nur du selbst (und die Leitung).",
        _ => "",
    };

    public static string Icon(AppointmentVisibilityLevel level) => level switch
    {
        AppointmentVisibilityLevel.Public => Icons.Material.Filled.Public,
        AppointmentVisibilityLevel.Restricted => Icons.Material.Filled.Lock,
        AppointmentVisibilityLevel.Private => Icons.Material.Filled.PersonOff,
        _ => Icons.Material.Filled.Event,
    };

    public static readonly IReadOnlyList<AppointmentVisibilityLevel> All = new[]
    {
        AppointmentVisibilityLevel.Public,
        AppointmentVisibilityLevel.Restricted,
        AppointmentVisibilityLevel.Private,
    };
}
