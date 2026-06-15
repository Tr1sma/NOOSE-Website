using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Common;

/// <summary>
/// Eine vom Agent gespeicherte Suche (Smart-Liste): Name + serialisierte Suchkriterien.
/// Gehört einem Agent (FK), keine Akte → hart löschbar (kein Soft-Delete), aber auditiert.
/// </summary>
[Table("GespeicherteSuchen")]
public class SavedSearch : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Identity-Schlüssel des Eigentümer-Agents.</summary>
    public string AgentId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Serialisierte <c>SuchKriterien</c> (JSON).</summary>
    [Column("SuchparameterJson")]
    public string SearchParameterJson { get; set; } = string.Empty;

    // ---- IAuditable ----
    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }
}
