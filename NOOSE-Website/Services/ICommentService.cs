using System.Security.Claims;
using NOOSE_Website.Data.Entities.Common;

namespace NOOSE_Website.Services;

/// <summary>Polymorphic comments on any record (via EntityType + EntityId); visibility follows the parent record, delete is soft-delete.</summary>
public interface ICommentService
{
    Task<List<Comment>> GetForRecordAsync(string entityType, string entityId, ViewerScope scope, CancellationToken cancellationToken = default);

    /// <summary>Internal-agent overload (print pages); not partner-aware.</summary>
    Task<List<Comment>> GetForRecordAsync(string entityType, string entityId, bool isLeadership, CancellationToken cancellationToken = default);

    Task<Comment> CreateAsync(string entityType, string entityId, string text, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task DeleteAsync(string commentId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
