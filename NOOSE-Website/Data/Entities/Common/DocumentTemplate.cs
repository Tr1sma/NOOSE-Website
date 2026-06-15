using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Common;

/// <summary>
/// Vorlage für ein Bibliotheks-<see cref="Dokument"/>: ein vordefinierter, formatierter HTML-Body, der
/// beim Anlegen eines neuen Dokuments in den Editor übernommen wird. Der Body darf Platzhalter
/// (z. B. <c>{{Name}}</c>, <c>{{Aktenzeichen}}</c>, <c>{{Datum}}</c>, <c>{{Agent}}</c>) enthalten, die der
/// <c>PlatzhalterService</c> beim Übernehmen aus dem Akten-/Nutzer-Kontext ersetzt. Führungs-verwaltet,
/// voll auditiert und papierkorbfähig (analog zur <c>DokVorlage</c>).
/// </summary>
[Table("DokumentVorlagen")]
public class DocumentTemplate : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Sprechender Name der Vorlage, z. B. „Vernehmungsprotokoll". Eindeutig (Dienst-geprüft).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optionale Erläuterung – in der Verwaltung und im Vorlagen-Picker angezeigt.</summary>
    [Column("Beschreibung")]
    public string? Description { get; set; }

    /// <summary>Optionale Kategorie zur Gruppierung.</summary>
    [Column("Kategorie")]
    public string? Category { get; set; }

    /// <summary>Bereinigter HTML-Body der Vorlage (darf Platzhalter-Tokens enthalten).</summary>
    [Column("InhaltHtml")]
    public string ContentHtml { get; set; } = string.Empty;

    /// <summary>Nur aktive Vorlagen erscheinen im Picker beim Dokument-Anlegen.</summary>
    [Column("IstAktiv")]
    public bool IsActive { get; set; } = true;

    /// <summary>Sortierreihenfolge im Picker/der Liste (kleiner zuerst).</summary>
    [Column("Sortierung")]
    public int Sorting { get; set; }

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
