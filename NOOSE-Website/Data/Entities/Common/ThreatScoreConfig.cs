using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Common;

/// <summary>Admin-tunable threat-score config as a single "global" row holding JSON, so new params need no migration (missing fields fall back to code defaults). No row = full default.</summary>
[Table("BedrohungsScoreKonfigs")]
public class ThreatScoreConfig : IAuditable
{
    public const string GlobalId = "global";

    public string Id { get; set; } = GlobalId;

    /// <summary>Serialized threat-score configuration (JSON, longtext).</summary>
    public string? Json { get; set; }

    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }
}
