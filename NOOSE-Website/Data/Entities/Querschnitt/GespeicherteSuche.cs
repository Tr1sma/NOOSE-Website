using NOOSE_Website.Models.Abstractions;

namespace NOOSE_Website.Data.Entities.Querschnitt;

/// <summary>
/// Eine vom Agent gespeicherte Suche (Smart-Liste): Name + serialisierte Suchkriterien.
/// Gehört einem Agent (FK), keine Akte → hart löschbar (kein Soft-Delete), aber auditiert.
/// </summary>
public class GespeicherteSuche : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Identity-Schlüssel des Eigentümer-Agents.</summary>
    public string AgentId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Serialisierte <c>SuchKriterien</c> (JSON).</summary>
    public string SuchparameterJson { get; set; } = string.Empty;

    // ---- IAuditable ----
    public DateTime ErstelltAm { get; set; }
    public string? ErstelltVonId { get; set; }
    public DateTime? GeaendertAm { get; set; }
    public string? GeaendertVonId { get; set; }
}
