using MudBlazor;

namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Sichtbarkeitsstufe eines Termins – Phase 8 (Block C, Erweiterung). Steuert, wer ihn sieht und in welchem
/// Kalender er erscheint. Die Aufsicht/Führung (<c>DarfVerschlusssacheLesen()</c>) sieht alle Stufen.
/// Bewusst anders benannt als der Dienst <c>TerminSichtbarkeit</c> (Namenskollision).
/// </summary>
public enum AppointmentVisibilityLevel
{
    /// <summary>Öffentlich: erscheint im Behörden-Kalender, für alle aktiven Agenten sichtbar.</summary>
    Public = 0,
    /// <summary>Eingeschränkt: nur Ersteller, zugeteilte Teilnehmer und die Aufsicht.</summary>
    Restricted = 1,
    /// <summary>Privat: nur der Ersteller (und die Aufsicht).</summary>
    Private = 2,
}

/// <summary>Anzeige-Helfer für die Termin-Sichtbarkeitsstufe.</summary>
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
