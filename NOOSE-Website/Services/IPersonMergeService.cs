using System.Security.Claims;

namespace NOOSE_Website.Services;

/// <summary>Merges a duplicate person record into the target (moving all children) and trashes the source. Leadership only.</summary>
public interface IPersonMergeService
{
    Task MergeAsync(string sourceId, string targetId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
