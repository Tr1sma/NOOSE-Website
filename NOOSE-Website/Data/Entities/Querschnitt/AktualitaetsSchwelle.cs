using NOOSE_Website.Models.Abstractions;

namespace NOOSE_Website.Data.Entities.Querschnitt;

/// <summary>
/// Admin-konfigurierbarer Schwellwert der Aktualitäts-Ampel je Aktentyp: ab <see cref="WarnungTage"/> Tagen
/// ohne Änderung wird eine Akte „gelb", ab <see cref="VeraltetTage"/> Tagen „rot". Eine Zeile je Aktentyp
/// (Primärschlüssel <see cref="AktenTyp"/>, z. B. <c>nameof(Person)</c>). Fehlt eine Zeile, gilt der im Code
/// hinterlegte Standardwert (siehe <c>AktualitaetService</c>). Reine Konfiguration – nur auditiert, kein
/// Soft-Delete.
/// </summary>
public class AktualitaetsSchwelle : IAuditable
{
    /// <summary>Aktentyp als Schlüssel, z. B. <c>nameof(Person)</c>.</summary>
    public string AktenTyp { get; set; } = string.Empty;

    /// <summary>Alter in Tagen, ab dem die Ampel auf „gelb" (Warnung) springt.</summary>
    public int WarnungTage { get; set; }

    /// <summary>Alter in Tagen, ab dem die Ampel auf „rot" (Veraltet) springt.</summary>
    public int VeraltetTage { get; set; }

    // ---- IAuditable ----
    public DateTime ErstelltAm { get; set; }
    public string? ErstelltVonId { get; set; }
    public DateTime? GeaendertAm { get; set; }
    public string? GeaendertVonId { get; set; }
}
