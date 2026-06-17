using System.Security.Claims;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <summary>Typed person-to-person relations from one person's view (regardless of stored side); hides trashed counterparts and classified ones for non-leadership.</summary>
public interface IRelationService
{
    /// <summary>Person-to-person relations from one person's view; partners see only relations to released persons.</summary>
    Task<List<RelationDisplay>> GetForPersonAsync(string personId, ViewerScope scope, CancellationToken cancellationToken = default);

    Task CreateAsync(string personAId, string personBId, RelationType type, string? note, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task RemoveAsync(string relationId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
