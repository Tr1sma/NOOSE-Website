using System.Security.Claims;
using System.Text.Json;
using NOOSE_Website.Authorization;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Builds audit rows for writes that bypass the SaveChanges interceptor (bulk/raw SQL, non-auditable entities).</summary>
public static class ManualAudit
{
    /// <summary>Audit row for one action against a record; log against the record so it also surfaces on its timeline.</summary>
    public static AuditLog Row(string entityType, string entityId, AuditAction action,
        ClaimsPrincipal actor, IReadOnlyDictionary<string, object?[]>? changes = null) => new()
    {
        Timestamp = DateTime.UtcNow,
        AgentId = actor.GetAgentId(),
        AgentName = actor.GetCodename(),
        EntityType = entityType,
        EntityId = entityId,
        Action = action,
        ChangesJson = changes is { Count: > 0 } ? JsonSerializer.Serialize(changes) : null,
    };

    /// <summary>One-field change map in the {field:[old,new]} shape the audit viewer expects.</summary>
    public static Dictionary<string, object?[]> Change(string field, object? oldValue, object? newValue)
        => new() { [field] = new[] { oldValue, newValue } };
}
