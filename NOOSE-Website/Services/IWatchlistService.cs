using System.Security.Claims;
using NOOSE_Website.Data.Entities.Watchlist;

namespace NOOSE_Website.Services;

/// <summary>Watchlist: an agent follows a record and is notified when it changes; unfollow is soft-delete.</summary>
public interface IWatchlistService
{
    /// <summary>Caller follows the record (no-op if already followed); gated on visibility.</summary>
    Task FollowAsync(string entityType, string entityId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Caller unfollows the record (soft-delete; no-op if not followed).</summary>
    Task UnfollowAsync(string entityType, string entityId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>True if the caller currently follows the record.</summary>
    Task<bool> IsFollowedAsync(string entityType, string entityId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>The caller's active follow entries, newest first.</summary>
    Task<List<WatchlistEntry>> GetFollowedAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Followed records resolved to display name + href; inaccessible ones stay listed but without name/link so they can be unfollowed.</summary>
    Task<List<FollowedRecord>> GetFollowedResolvedAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Agent ids of all active followers of a record (for the notification fan-out).</summary>
    Task<List<string>> GetFollowerIdsAsync(string entityType, string entityId, CancellationToken cancellationToken = default);
}

/// <summary>A followed record resolved for display.</summary>
public sealed record FollowedRecord(string Type, string Id, string Display, string? Href, DateTime CreatedAt, bool Accessible);
