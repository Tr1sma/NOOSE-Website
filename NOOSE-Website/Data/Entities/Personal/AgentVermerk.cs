using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Data.Entities.Personal;

/// <summary>
/// Ein Personalakten-Vermerk zu einem Agent (Phase 5e): Belobigung oder Disziplinar-Eintrag. Datierter,
/// auditierter Eintrag mit Autor (Vorlage: <c>Kommentar</c>). Für alle Agenten sichtbar; anlegen/löschen nur
/// durch die Führung. <see cref="AutorName"/> = Codename des Verfassers (denormalisiert).
/// </summary>
public class AgentVermerk : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string AgentId { get; set; } = string.Empty;

    public AgentVermerkArt Art { get; set; }

    public string Text { get; set; } = string.Empty;

    /// <summary>Codename des Verfassers zum Zeitpunkt (denormalisiert).</summary>
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
