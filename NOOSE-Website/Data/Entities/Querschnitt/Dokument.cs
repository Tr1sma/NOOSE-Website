using NOOSE_Website.Models.Abstractions;

namespace NOOSE_Website.Data.Entities.Querschnitt;

/// <summary>
/// Ein eigenständiges Dokument der zentralen Dokumenten-Bibliothek: ein im WYSIWYG-Editor erstellter,
/// formatierter Text, der serverseitig bereinigt als HTML (<see cref="InhaltHtml"/>) abgelegt wird.
/// Dokumente sind wiederverwendbar und werden über generische Quellen (<see cref="Quelle"/> mit
/// <c>Typ = QuelleTyp.Dokument</c>, <c>ZielTyp = nameof(Dokument)</c>, <c>ZielId = Id</c>) an eine oder
/// mehrere Akten angehängt. Voll auditiert und papierkorbfähig.
/// </summary>
public class Dokument : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Titel { get; set; } = string.Empty;

    /// <summary>Optionale Kategorie zur Gruppierung/Filterung in der Bibliothek (z. B. „SOP", „Formular").</summary>
    public string? Kategorie { get; set; }

    /// <summary>Bereinigter HTML-Inhalt (serverseitig durch <c>HtmlBereinigung</c> gefiltert).</summary>
    public string InhaltHtml { get; set; } = string.Empty;

    /// <summary>Verschlusssache: nur für die Führung sichtbar (Bibliothek/Viewer/Auswahl filtern entsprechend).</summary>
    public bool IstVerschlusssache { get; set; }

    /// <summary>Angepinnt: erscheint in der Bibliothek in einem abgesetzten Block ganz oben. Globale, von der
    /// Führung kuratierte Markierung (kein „zuletzt bearbeitet"-Bezug). Steuert nur die Anzeige-Reihenfolge.</summary>
    public bool Angepinnt { get; set; }

    // ---- IAuditable ----
    public DateTime ErstelltAm { get; set; }
    public string? ErstelltVonId { get; set; }
    public DateTime? GeaendertAm { get; set; }
    public string? GeaendertVonId { get; set; }

    // ---- ISoftDelete ----
    public bool IstGeloescht { get; set; }
    public DateTime? GeloeschtAm { get; set; }
    public string? GeloeschtVonId { get; set; }
}
