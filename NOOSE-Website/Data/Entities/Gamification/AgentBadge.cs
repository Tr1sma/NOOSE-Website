using System.ComponentModel.DataAnnotations.Schema;
using NOOSE_Website.Models.Abstractions;

namespace NOOSE_Website.Data.Entities.Gamification;

/// <summary>An awarded milestone badge for an agent; append-only, one per (agent, key).</summary>
[Table("Auszeichnungen")]
public class AgentBadge : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string AgentId { get; set; } = string.Empty;

    [Column("AbzeichenSchluessel")]
    public string BadgeKey { get; set; } = string.Empty;

    [Column("VerliehenAm")]
    public DateTime AwardedAt { get; set; }

    [Column("Notiz")]
    public string? Note { get; set; }

    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }
}
