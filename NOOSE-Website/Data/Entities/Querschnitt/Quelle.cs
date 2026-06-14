using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Querschnitt;

/// <summary>
/// Eine generische Quelle/ein Anhang an einer beliebigen Akte (Person; ab Phase 4 auch Fraktion/Gruppe …).
/// Die Zuordnung erfolgt polymorph über <see cref="EntitaetTyp"/> + <see cref="EntitaetId"/> – analog zum
/// Audit-/Zugriffs-Log, daher ohne FK-Navigation. Vier Ausprägungen über <see cref="Typ"/>:
/// Datei-Upload, Web-Link, interne Verknüpfung oder Freitext. Voll auditiert und papierkorbfähig.
/// </summary>
[Table("Quellen")]
public class Quelle : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Typ der Eltern-Akte, z. B. <c>nameof(Person)</c>.</summary>
    [Column("EntitaetTyp")]
    public string EntitaetTyp { get; set; } = string.Empty;

    /// <summary>Schlüssel der Eltern-Akte.</summary>
    [Column("EntitaetId")]
    public string EntitaetId { get; set; } = string.Empty;

    [Column("Typ")]
    public QuelleTyp Typ { get; set; }

    [Column("Titel")]
    public string Titel { get; set; } = string.Empty;

    /// <summary>Angepinnt: erscheint in der Quellenliste der Akte ganz oben (vor allen nicht
    /// angepinnten). Steuert nur die Anzeige-Reihenfolge, kein „zuletzt bearbeitet"-Bezug.</summary>
    [Column("Angepinnt")]
    public bool Angepinnt { get; set; }

    /// <summary>Freitext-Inhalt bzw. Notiz zur Quelle.</summary>
    [Column("Beschreibung")]
    public string? Beschreibung { get; set; }

    /// <summary>Ziel-URL bei <see cref="QuelleTyp.Link"/>.</summary>
    public string? Url { get; set; }

    /// <summary>Verweis-Typ bei <see cref="QuelleTyp.Intern"/> (z. B. <c>nameof(Person)</c>).</summary>
    [Column("ZielTyp")]
    public string? ZielTyp { get; set; }

    /// <summary>Verweis-Schlüssel bei <see cref="QuelleTyp.Intern"/>.</summary>
    [Column("ZielId")]
    public string? ZielId { get; set; }

    // ---- Datei-Metadaten bei QuelleTyp.Upload (Datei liegt geschützt außerhalb wwwroot) ----
    [Column("DateinameGespeichert")]
    public string? DateinameGespeichert { get; set; }
    public string? OriginalName { get; set; }
    public string? ContentType { get; set; }
    [Column("GroesseBytes")]
    public long? GroesseBytes { get; set; }

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
