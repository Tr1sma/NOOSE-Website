using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Data.Entities.Querschnitt;

/// <summary>
/// Eine generische Quelle/ein Anhang an einer beliebigen Akte (Person; ab Phase 4 auch Fraktion/Gruppe …).
/// Die Zuordnung erfolgt polymorph über <see cref="EntitaetTyp"/> + <see cref="EntitaetId"/> – analog zum
/// Audit-/Zugriffs-Log, daher ohne FK-Navigation. Vier Ausprägungen über <see cref="Typ"/>:
/// Datei-Upload, Web-Link, interne Verknüpfung oder Freitext. Voll auditiert und papierkorbfähig.
/// </summary>
public class Quelle : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Typ der Eltern-Akte, z. B. <c>nameof(Person)</c>.</summary>
    public string EntitaetTyp { get; set; } = string.Empty;

    /// <summary>Schlüssel der Eltern-Akte.</summary>
    public string EntitaetId { get; set; } = string.Empty;

    public QuelleTyp Typ { get; set; }

    public string Titel { get; set; } = string.Empty;

    /// <summary>Angepinnt: erscheint in der Quellenliste der Akte ganz oben (vor allen nicht
    /// angepinnten). Steuert nur die Anzeige-Reihenfolge, kein „zuletzt bearbeitet"-Bezug.</summary>
    public bool Angepinnt { get; set; }

    /// <summary>Freitext-Inhalt bzw. Notiz zur Quelle.</summary>
    public string? Beschreibung { get; set; }

    /// <summary>Ziel-URL bei <see cref="QuelleTyp.Link"/>.</summary>
    public string? Url { get; set; }

    /// <summary>Verweis-Typ bei <see cref="QuelleTyp.Intern"/> (z. B. <c>nameof(Person)</c>).</summary>
    public string? ZielTyp { get; set; }

    /// <summary>Verweis-Schlüssel bei <see cref="QuelleTyp.Intern"/>.</summary>
    public string? ZielId { get; set; }

    // ---- Datei-Metadaten bei QuelleTyp.Upload (Datei liegt geschützt außerhalb wwwroot) ----
    public string? DateinameGespeichert { get; set; }
    public string? OriginalName { get; set; }
    public string? ContentType { get; set; }
    public long? GroesseBytes { get; set; }

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
