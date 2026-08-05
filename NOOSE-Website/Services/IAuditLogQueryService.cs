using System.Security.Claims;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <summary>Read access to the change- and access-logs for leadership and read-only supervision, with filtering and a row cap.</summary>
public interface IAuditLogQueryService
{
    /// <summary>Filtered change-log rows (newest first), capped; leadership or read-only supervision.</summary>
    Task<AuditLogPage> QueryChangesAsync(AuditLogFilter filter, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Filtered access-log rows (newest first), capped; leadership or read-only supervision.</summary>
    Task<AccessLogPage> QueryAccessAsync(AuditLogFilter filter, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Dropdown values (agents, entity types) for the filter bar; leadership or read-only supervision.</summary>
    Task<AuditFilterOptions> GetFilterOptionsAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
