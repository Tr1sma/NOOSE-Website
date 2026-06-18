using System.Security.Claims;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <summary>Generic link engine: directed links between records, returned bidirectionally normalized from one record's view; visibility-checked per record.</summary>
public interface ILinkService
{
    /// <summary>Links of a record, bidirectionally normalized; optionally restricted to one kind. Partners see only links to released records.</summary>
    Task<List<LinkDisplay>> GetForRecordAsync(string entityType, string entityId, ViewerScope scope, LinkKind? kind = null, CancellationToken cancellationToken = default);

    /// <summary>Internal-agent overload (print pages and non-partner panels); not partner-aware.</summary>
    Task<List<LinkDisplay>> GetForRecordAsync(string entityType, string entityId, bool isLeadership, string? meId, LinkKind? kind = null, CancellationToken cancellationToken = default);

    Task CreateAsync(string sourceType, string sourceId, string targetType, string targetId, string? label, ClaimsPrincipal actor, LinkKind kind = LinkKind.Default, CancellationToken cancellationToken = default);

    Task RemoveAsync(string linkId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
