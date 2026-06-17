using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Personnel;

/// <summary>Append-only rank-history entry; written on every rank change. No soft-delete.</summary>
[Table("AgentDienstgradVerlaeufe")]
public class AgentRankHistory
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string AgentId { get; set; } = string.Empty;

    /// <summary>Previous rank; null on first release.</summary>
    public Rank? Alt { get; set; }

    [Column("Neu")]
    public Rank New { get; set; }

    [Column("Zeitpunkt")]
    public DateTime Timestamp { get; set; }

    [Column("AkteurName")]
    public string? ActorName { get; set; }

    [Column("Grund")]
    public string? Reason { get; set; }
}
