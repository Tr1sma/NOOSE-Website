using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Common;

/// <summary>
/// Eine generische Quelle/ein Anhang an einer beliebigen Akte (Person; ab Phase 4 auch Fraktion/Gruppe …).
/// Die Zuordnung erfolgt polymorph über <see cref="EntitaetTyp"/> + <see cref="EntitaetId"/> – analog zum
/// Audit-/Zugriffs-Log, daher ohne FK-Navigation. Vier Ausprägungen über <see cref="Typ"/>:
/// Datei-Upload, Web-Link, interne Verknüpfung oder Freitext. Voll auditiert und papierkorbfähig.
/// </summary>
[Table("Quellen")]
public class Source : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Typ der Eltern-Akte, z. B. <c>nameof(Person)</c>.</summary>
    [Column("EntitaetTyp")]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Schlüssel der Eltern-Akte.</summary>
    [Column("EntitaetId")]
    public string EntityId { get; set; } = string.Empty;

    [Column("Typ")]
    public SourceType Type { get; set; }

    [Column("Titel")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Angepinnt: erscheint in der Quellenliste der Akte ganz oben (vor allen nicht
    /// angepinnten). Steuert nur die Anzeige-Reihenfolge, kein „zuletzt bearbeitet"-Bezug.</summary>
    [Column("Angepinnt")]
    public bool Pinned { get; set; }

    /// <summary>Freitext-Inhalt bzw. Notiz zur Quelle.</summary>
    [Column("Beschreibung")]
    public string? Description { get; set; }

    /// <summary>Ziel-URL bei <see cref="QuelleTyp.Link"/>.</summary>
    public string? Url { get; set; }

    /// <summary>Verweis-Typ bei <see cref="QuelleTyp.Intern"/> (z. B. <c>nameof(Person)</c>).</summary>
    [Column("ZielTyp")]
    public string? TargetType { get; set; }

    /// <summary>Verweis-Schlüssel bei <see cref="QuelleTyp.Intern"/>.</summary>
    [Column("ZielId")]
    public string? TargetId { get; set; }

    // ---- Datei-Metadaten bei QuelleTyp.Upload (Datei liegt geschützt außerhalb wwwroot) ----
    [Column("DateinameGespeichert")]
    public string? FileNameSaved { get; set; }
    public string? OriginalName { get; set; }
    public string? ContentType { get; set; }
    [Column("GroesseBytes")]
    public long? SizeBytes { get; set; }

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
