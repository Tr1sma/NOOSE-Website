using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Querschnitt;

/// <summary>
/// Ein eigenständiges Dokument der zentralen Dokumenten-Bibliothek: ein im WYSIWYG-Editor erstellter,
/// formatierter Text, der serverseitig bereinigt als HTML (<see cref="InhaltHtml"/>) abgelegt wird.
/// Dokumente sind wiederverwendbar und werden über generische Quellen (<see cref="Quelle"/> mit
/// <c>Typ = QuelleTyp.Dokument</c>, <c>ZielTyp = nameof(Dokument)</c>, <c>ZielId = Id</c>) an eine oder
/// mehrere Akten angehängt. Voll auditiert und papierkorbfähig.
/// </summary>
[Table("Dokumente")]
public class Dokument : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("Titel")]
    public string Titel { get; set; } = string.Empty;

    /// <summary>Optionale Kategorie zur Gruppierung/Filterung in der Bibliothek (z. B. „SOP", „Formular").</summary>
    [Column("Kategorie")]
    public string? Kategorie { get; set; }

    /// <summary>Bereinigter HTML-Inhalt (serverseitig durch <c>HtmlBereinigung</c> gefiltert).</summary>
    [Column("InhaltHtml")]
    public string InhaltHtml { get; set; } = string.Empty;

    /// <summary>Verschlusssache: nur für die Führung sichtbar (Bibliothek/Viewer/Auswahl filtern entsprechend).</summary>
    [Column("IstVerschlusssache")]
    public bool IstVerschlusssache { get; set; }

    /// <summary>Angepinnt: erscheint in der Bibliothek in einem abgesetzten Block ganz oben. Globale, von der
    /// Führung kuratierte Markierung (kein „zuletzt bearbeitet"-Bezug). Steuert nur die Anzeige-Reihenfolge.</summary>
    [Column("Angepinnt")]
    public bool Angepinnt { get; set; }

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
