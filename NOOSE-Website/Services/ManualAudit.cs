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

    /// <summary>Audit row for a background sweep; there is no actor, so it is logged as the system.</summary>
    /// <remarks>Mirrors what CurrentUserInfo.System stamps on a tracked save, so a swept row does not read
    /// as if the last human to touch the record had done it.</remarks>
    public static AuditLog SystemRow(string entityType, string entityId, AuditAction action,
        IReadOnlyDictionary<string, object?[]>? changes = null) => new()
    {
        Timestamp = DateTime.UtcNow,
        AgentId = null,
        AgentName = "System",
        EntityType = entityType,
        EntityId = entityId,
        Action = action,
        ChangesJson = changes is { Count: > 0 } ? JsonSerializer.Serialize(changes) : null,
    };
}
