using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Common;

/// <summary>
/// Eine hochgeladene Datei der zentralen Datei-Bibliothek (Formulare, SOPs, Vorlagen – Phase 7).
/// Die Datei selbst liegt außerhalb von wwwroot (siehe <c>IBibliothekStorageService</c>) und wird
/// nur über den geschützten Endpoint <c>/dateien/bibliothek/{id}</c> ausgeliefert. Verschlusssachen
/// sind nur der Führung sichtbar. Voll auditiert und papierkorbfähig.
/// </summary>
[Table("BibliothekDateien")]
public class LibraryFile : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("Titel")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Optionale Kategorie zur Gruppierung/Filterung (z. B. „Formular", „SOP").</summary>
    [Column("Kategorie")]
    public string? Category { get; set; }

    /// <summary>Ursprünglicher Dateiname des Uploads (für den Download-Dateinamen).</summary>
    public string OriginalName { get; set; } = string.Empty;

    /// <summary>Serverseitig vergebener Dateiname im Bibliotheks-Ordner (GUID + sichere Endung).</summary>
    [Column("DateinameGespeichert")]
    public string FileNameSaved { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    [Column("GroesseBytes")]
    public long SizeBytes { get; set; }

    /// <summary>Verschlusssache: nur für die Führung sichtbar.</summary>
    [Column("IstVerschlusssache")]
    public bool IsClassified { get; set; }

    // ---- IAuditable ----
    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }

    // ---- ISoftDelete ----
    [Column("IstGeloescht")]
    public bool IsDeleted { get; set; }
    [Column("GeloeschtAm")]
    public DateTime? DeletedAt { get; set; }
    [Column("GeloeschtVonId")]
    public string? DeletedById { get; set; }
}
