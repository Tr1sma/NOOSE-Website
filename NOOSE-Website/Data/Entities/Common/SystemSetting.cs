using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Common;

/// <summary>
/// Generische Schlüssel/Wert-Systemeinstellung (Wartungsmodus, Ankündigungsbanner, Theme-Farben,
/// Logo-Datei). Eine Zeile je Schlüssel (Primärschlüssel <see cref="Schluessel"/>; Konstanten in
/// <c>SystemEinstellungKeys</c>). Fehlt eine Zeile, gilt der Code-Standard. Reine Konfiguration –
/// nur auditiert, kein Soft-Delete (analog <see cref="AktualitaetsSchwelle"/>).
/// </summary>
[Table("SystemEinstellungen")]
public class SystemSetting : IAuditable
{
    /// <summary>Einstellungs-Schlüssel, z. B. <c>WartungsmodusAktiv</c>.</summary>
    [Column("Schluessel")]
    public string Key { get; set; } = string.Empty;

    /// <summary>Wert als Text (bool als "true"/"false", Farben als Hex, Dateinamen roh).</summary>
    [Column("Wert")]
    public string? Value { get; set; }

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
