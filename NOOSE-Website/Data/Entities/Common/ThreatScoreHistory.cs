using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Common;

/// <summary>Append-only threat-score snapshot, polymorphic via EntityType + EntityId across Person and Faction.
/// Deliberately NOT IAuditable/ISoftDelete — every recompute would otherwise stamp an audit row.</summary>
[Table("BedrohungsScoreVerlauf")]
public class ThreatScoreHistory
{
    public long Id { get; set; }

    /// <summary>CLR type name of the record (nameof(Person) / nameof(Faction)).</summary>
    [Column("EntitaetTyp")]
    public string EntityType { get; set; } = string.Empty;

    [Column("EntitaetId")]
    public string EntityId { get; set; } = string.Empty;

    /// <summary>Score at this point in time; null = not scored/excluded.</summary>
    [Column("Score")]
    public int? Score { get; set; }

    [Column("Konfidenz")]
    public int? Confidence { get; set; }

    /// <summary>Frozen score-detail JSON snapshot.</summary>
    [Column("DetailJson")]
    public string? DetailJson { get; set; }

    [Column("Zeitpunkt")]
    public DateTime Timestamp { get; set; }
}
