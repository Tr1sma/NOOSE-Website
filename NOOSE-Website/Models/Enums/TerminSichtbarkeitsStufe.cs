using MudBlazor;

namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Sichtbarkeitsstufe eines Termins – Phase 8 (Block C, Erweiterung). Steuert, wer ihn sieht und in welchem
/// Kalender er erscheint. Die Aufsicht/Führung (<c>DarfVerschlusssacheLesen()</c>) sieht alle Stufen.
/// Bewusst anders benannt als der Dienst <c>TerminSichtbarkeit</c> (Namenskollision).
/// </summary>
public enum TerminSichtbarkeitsStufe
{
    /// <summary>Öffentlich: erscheint im Behörden-Kalender, für alle aktiven Agenten sichtbar.</summary>
    Oeffentlich = 0,
    /// <summary>Eingeschränkt: nur Ersteller, zugeteilte Teilnehmer und die Aufsicht.</summary>
    Eingeschraenkt = 1,
    /// <summary>Privat: nur der Ersteller (und die Aufsicht).</summary>
    Privat = 2,
}

/// <summary>Anzeige-Helfer für die Termin-Sichtbarkeitsstufe.</summary>
public static class TerminSichtbarkeitsStufeAnzeige
{
    public static string Name(TerminSichtbarkeitsStufe stufe) => stufe switch
    {
        TerminSichtbarkeitsStufe.Oeffentlich => "Öffentlich",
        TerminSichtbarkeitsStufe.Eingeschraenkt => "Eingeschränkt",
        TerminSichtbarkeitsStufe.Privat => "Privat",
        _ => "—",
    };

    public static string Hilfe(TerminSichtbarkeitsStufe stufe) => stufe switch
    {
        TerminSichtbarkeitsStufe.Oeffentlich => "Im Behörden-Kalender für alle aktiven Agenten sichtbar.",
        TerminSichtbarkeitsStufe.Eingeschraenkt => "Nur Ersteller, zugeteilte Teilnehmer und die Leitung.",
        TerminSichtbarkeitsStufe.Privat => "Nur du selbst (und die Leitung).",
        _ => "",
    };

    public static string Icon(TerminSichtbarkeitsStufe stufe) => stufe switch
    {
        TerminSichtbarkeitsStufe.Oeffentlich => Icons.Material.Filled.Public,
        TerminSichtbarkeitsStufe.Eingeschraenkt => Icons.Material.Filled.Lock,
        TerminSichtbarkeitsStufe.Privat => Icons.Material.Filled.PersonOff,
        _ => Icons.Material.Filled.Event,
    };

    public static readonly IReadOnlyList<TerminSichtbarkeitsStufe> Alle = new[]
    {
        TerminSichtbarkeitsStufe.Oeffentlich,
        TerminSichtbarkeitsStufe.Eingeschraenkt,
        TerminSichtbarkeitsStufe.Privat,
    };
}
