using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Common;

/// <summary>
/// Ein eigenständiges Dokument der zentralen Dokumenten-Bibliothek: ein im WYSIWYG-Editor erstellter,
/// formatierter Text, der serverseitig bereinigt als HTML (<see cref="InhaltHtml"/>) abgelegt wird.
/// Dokumente sind wiederverwendbar und werden über generische Quellen (<see cref="Quelle"/> mit
/// <c>Typ = QuelleTyp.Dokument</c>, <c>ZielTyp = nameof(Dokument)</c>, <c>ZielId = Id</c>) an eine oder
/// mehrere Akten angehängt. Voll auditiert und papierkorbfähig.
/// </summary>
[Table("Dokumente")]
public class Document : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("Titel")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Optionale Kategorie zur Gruppierung/Filterung in der Bibliothek (z. B. „SOP", „Formular").</summary>
    [Column("Kategorie")]
    public string? Category { get; set; }

    /// <summary>Bereinigter HTML-Inhalt (serverseitig durch <c>HtmlBereinigung</c> gefiltert).</summary>
    [Column("InhaltHtml")]
    public string ContentHtml { get; set; } = string.Empty;

    /// <summary>Verschlusssache: nur für die Führung sichtbar (Bibliothek/Viewer/Auswahl filtern entsprechend).</summary>
    [Column("IstVerschlusssache")]
    public bool IsClassified { get; set; }

    /// <summary>Verschlusssache nur für die TRU (Tactical Response Unit). Schließt sich mit den übrigen
    /// Verschluss-Stufen gegenseitig aus (gesetzt über <see cref="Classification"/>).</summary>
    [Column("IstVerschlusssacheTRU")]
    public bool IsTRUClassified { get; set; }

    /// <summary>Verschlusssache nur für den HRB (Human Resources Branch). Schließt sich mit den übrigen
    /// Verschluss-Stufen gegenseitig aus (gesetzt über <see cref="Classification"/>).</summary>
    [Column("IstVerschlusssacheHRB")]
    public bool IsHRBClassified { get; set; }

    /// <summary>Einheitliche Verschluss-Stufe (Mapping auf genau eine der Bool-Spalten bzw. keine).
    /// Führung hat beim Lesen Vorrang, falls je mehrere Flags gesetzt wären.</summary>
    [NotMapped]
    public DocumentClassification Classification
    {
        get => IsClassified ? DocumentClassification.Leadership
            : IsTRUClassified ? DocumentClassification.Tru
            : IsHRBClassified ? DocumentClassification.Hrb
            : DocumentClassification.None;
        set
        {
            IsClassified = value == DocumentClassification.Leadership;
            IsTRUClassified = value == DocumentClassification.Tru;
            IsHRBClassified = value == DocumentClassification.Hrb;
        }
    }

    /// <summary>True, wenn überhaupt eine Verschluss-Stufe gesetzt ist (für die Schloss-Anzeige).</summary>
    [NotMapped]
    public bool IsRestricted => IsClassified || IsTRUClassified || IsHRBClassified;

    /// <summary>Angepinnt: erscheint in der Bibliothek in einem abgesetzten Block ganz oben. Globale, von der
    /// Führung kuratierte Markierung (kein „zuletzt bearbeitet"-Bezug). Steuert nur die Anzeige-Reihenfolge.</summary>
    [Column("Angepinnt")]
    public bool Pinned { get; set; }

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
