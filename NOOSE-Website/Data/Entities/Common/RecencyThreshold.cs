using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Common;

/// <summary>
/// Admin-konfigurierbarer Schwellwert der Aktualitäts-Ampel je Aktentyp: ab <see cref="WarnungTage"/> Tagen
/// ohne Änderung wird eine Akte „gelb", ab <see cref="VeraltetTage"/> Tagen „rot". Eine Zeile je Aktentyp
/// (Primärschlüssel <see cref="AktenTyp"/>, z. B. <c>nameof(Person)</c>). Fehlt eine Zeile, gilt der im Code
/// hinterlegte Standardwert (siehe <c>AktualitaetService</c>). Reine Konfiguration – nur auditiert, kein
/// Soft-Delete.
/// </summary>
[Table("AktualitaetsSchwellen")]
public class RecencyThreshold : IAuditable
{
    /// <summary>Aktentyp als Schlüssel, z. B. <c>nameof(Person)</c>.</summary>
    [Column("AktenTyp")]
    public string RecordsType { get; set; } = string.Empty;

    /// <summary>Alter in Tagen, ab dem die Ampel auf „gelb" (Warnung) springt.</summary>
    [Column("WarnungTage")]
    public int WarningDays { get; set; }

    /// <summary>Alter in Tagen, ab dem die Ampel auf „rot" (Veraltet) springt.</summary>
    [Column("VeraltetTage")]
    public int StaleDays { get; set; }

    // ---- IAuditable ----
    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }
}
