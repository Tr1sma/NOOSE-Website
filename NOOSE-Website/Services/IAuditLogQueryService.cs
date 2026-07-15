using System.Security.Claims;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <summary>Admin-only read access to the change- and access-logs, with filtering and a row cap.</summary>
public interface IAuditLogQueryService
{
    /// <summary>Filtered change-log rows (newest first), capped; admin-only.</summary>
    Task<AuditLogPage> QueryChangesAsync(AuditLogFilter filter, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Filtered access-log rows (newest first), capped; admin-only.</summary>
    Task<AccessLogPage> QueryAccessAsync(AuditLogFilter filter, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Dropdown values (agents, entity types) for the filter bar; admin-only.</summary>
    Task<AuditFilterOptions> GetFilterOptionsAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
