using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Querschnitt;

/// <summary>
/// Eine vom Agent gespeicherte Suche (Smart-Liste): Name + serialisierte Suchkriterien.
/// Gehört einem Agent (FK), keine Akte → hart löschbar (kein Soft-Delete), aber auditiert.
/// </summary>
[Table("GespeicherteSuchen")]
public class GespeicherteSuche : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Identity-Schlüssel des Eigentümer-Agents.</summary>
    public string AgentId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Serialisierte <c>SuchKriterien</c> (JSON).</summary>
    [Column("SuchparameterJson")]
    public string SuchparameterJson { get; set; } = string.Empty;

    // ---- IAuditable ----
    [Column("ErstelltAm")]
    public DateTime ErstelltAm { get; set; }
    [Column("ErstelltVonId")]
    public string? ErstelltVonId { get; set; }
    [Column("GeaendertAm")]
    public DateTime? GeaendertAm { get; set; }
    [Column("GeaendertVonId")]
    public string? GeaendertVonId { get; set; }
}
