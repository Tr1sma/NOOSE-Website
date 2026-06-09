using NOOSE_Website.Models.Abstractions;

namespace NOOSE_Website.Data.Entities.Querschnitt;

/// <summary>
/// Ein generischer Kommentar/Vermerk an einer beliebigen Akte (polymorph über <see cref="EntitaetTyp"/>
/// + <see cref="EntitaetId"/>). Voll auditiert und papierkorbfähig. @-Erwähnungen folgen erst in Phase 6.
/// </summary>
public class Kommentar : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string EntitaetTyp { get; set; } = string.Empty;
    public string EntitaetId { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    /// <summary>Codename des Autors zum Zeitpunkt der Erstellung (denormalisiert, wie EinstufungVerlauf.AgentName).</summary>
    public string? AutorName { get; set; }

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
