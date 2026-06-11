using NOOSE_Website.Models.Abstractions;

namespace NOOSE_Website.Data.Entities.Querschnitt;

/// <summary>
/// Vorlage für ein Bibliotheks-<see cref="Dokument"/>: ein vordefinierter, formatierter HTML-Body, der
/// beim Anlegen eines neuen Dokuments in den Editor übernommen wird. Der Body darf Platzhalter
/// (z. B. <c>{{Name}}</c>, <c>{{Aktenzeichen}}</c>, <c>{{Datum}}</c>, <c>{{Agent}}</c>) enthalten, die der
/// <c>PlatzhalterService</c> beim Übernehmen aus dem Akten-/Nutzer-Kontext ersetzt. Führungs-verwaltet,
/// voll auditiert und papierkorbfähig (analog zur <c>DokVorlage</c>).
/// </summary>
public class DokumentVorlage : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Sprechender Name der Vorlage, z. B. „Vernehmungsprotokoll". Eindeutig (Dienst-geprüft).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optionale Erläuterung – in der Verwaltung und im Vorlagen-Picker angezeigt.</summary>
    public string? Beschreibung { get; set; }

    /// <summary>Optionale Kategorie zur Gruppierung.</summary>
    public string? Kategorie { get; set; }

    /// <summary>Bereinigter HTML-Body der Vorlage (darf Platzhalter-Tokens enthalten).</summary>
    public string InhaltHtml { get; set; } = string.Empty;

    /// <summary>Nur aktive Vorlagen erscheinen im Picker beim Dokument-Anlegen.</summary>
    public bool IstAktiv { get; set; } = true;

    /// <summary>Sortierreihenfolge im Picker/der Liste (kleiner zuerst).</summary>
    public int Sortierung { get; set; }

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
