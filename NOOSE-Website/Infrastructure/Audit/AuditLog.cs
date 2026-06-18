using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Infrastructure.Audit;

/// <summary>Immutable change-log entry: who changed what on which entity, when. Written by <see cref="AuditSaveChangesInterceptor"/>.</summary>
[Table("AuditLogs")]
public class AuditLog
{
    public long Id { get; set; }
    [Column("Zeitpunkt")]
    public DateTime Timestamp { get; set; }

    /// <summary>Actor's agent id; null for system/background actions.</summary>
    public string? AgentId { get; set; }

    /// <summary>Actor's codename at action time (denormalized).</summary>
    public string? AgentName { get; set; }

    /// <summary>CLR type name of the affected entity.</summary>
    [Column("EntitaetTyp")]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Primary key of the affected entity as text.</summary>
    [Column("EntitaetId")]
    public string EntityId { get; set; } = string.Empty;

    [Column("Aktion")]
    public AuditAction Action { get; set; }

    /// <summary>JSON of changed fields (old → new); null for pure creations.</summary>
    [Column("AenderungenJson")]
    public string? ChangesJson { get; set; }
}
