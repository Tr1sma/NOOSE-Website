using System.ComponentModel.DataAnnotations.Schema;
namespace NOOSE_Website.Infrastructure.Audit;

/// <summary>Read/access log: who viewed which record and when.</summary>
[Table("ZugriffsLogs")]
public class AccessLog
{
    public long Id { get; set; }
    [Column("Zeitpunkt")]
    public DateTime Timestamp { get; set; }
    public string? AgentId { get; set; }
    public string? AgentName { get; set; }
    [Column("EntitaetTyp")]
    public string EntityType { get; set; } = string.Empty;
    [Column("EntitaetId")]
    public string EntityId { get; set; } = string.Empty;
}
