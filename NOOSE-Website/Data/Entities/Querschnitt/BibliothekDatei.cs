using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Querschnitt;

/// <summary>
/// Eine hochgeladene Datei der zentralen Datei-Bibliothek (Formulare, SOPs, Vorlagen – Phase 7).
/// Die Datei selbst liegt außerhalb von wwwroot (siehe <c>IBibliothekStorageService</c>) und wird
/// nur über den geschützten Endpoint <c>/dateien/bibliothek/{id}</c> ausgeliefert. Verschlusssachen
/// sind nur der Führung sichtbar. Voll auditiert und papierkorbfähig.
/// </summary>
[Table("BibliothekDateien")]
public class BibliothekDatei : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("Titel")]
    public string Titel { get; set; } = string.Empty;

    /// <summary>Optionale Kategorie zur Gruppierung/Filterung (z. B. „Formular", „SOP").</summary>
    [Column("Kategorie")]
    public string? Kategorie { get; set; }

    /// <summary>Ursprünglicher Dateiname des Uploads (für den Download-Dateinamen).</summary>
    public string OriginalName { get; set; } = string.Empty;

    /// <summary>Serverseitig vergebener Dateiname im Bibliotheks-Ordner (GUID + sichere Endung).</summary>
    [Column("DateinameGespeichert")]
    public string DateinameGespeichert { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    [Column("GroesseBytes")]
    public long GroesseBytes { get; set; }

    /// <summary>Verschlusssache: nur für die Führung sichtbar.</summary>
    [Column("IstVerschlusssache")]
    public bool IstVerschlusssache { get; set; }

    // ---- IAuditable ----
    [Column("ErstelltAm")]
    public DateTime ErstelltAm { get; set; }
    [Column("ErstelltVonId")]
    public string? ErstelltVonId { get; set; }
    [Column("GeaendertAm")]
    public DateTime? GeaendertAm { get; set; }
    [Column("GeaendertVonId")]
    public string? GeaendertVonId { get; set; }

    // ---- ISoftDelete ----
    [Column("IstGeloescht")]
    public bool IstGeloescht { get; set; }
    [Column("GeloeschtAm")]
    public DateTime? GeloeschtAm { get; set; }
    [Column("GeloeschtVonId")]
    public string? GeloeschtVonId { get; set; }
}
