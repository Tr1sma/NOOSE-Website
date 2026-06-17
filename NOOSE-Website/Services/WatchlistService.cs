using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Watchlist;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IWatchlistService" />
public class WatchlistService(IDbContextFactory<AppDbContext> dbFactory) : IWatchlistService
{
    public async Task FollowAsync(string entityType, string entityId, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        var agentId = actor.GetAgentId();
        if (string.IsNullOrWhiteSpace(agentId))
        {
            return;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // only follow what the caller may see
        if (!await Visibility.IsRecordVisibleAsync(db, entityType, entityId, actor.IsLeadership(), cancellationToken))
        {
            throw new UnauthorizedAccessException("Diese Akte ist für dich nicht zugänglich.");
        }

        var active = await db.Watchlists
            .FirstOrDefaultAsync(w => w.AgentId == agentId && w.EntityType == entityType && w.EntityId == entityId,
                cancellationToken);
        if (active is not null)
        {
            return;
        }

        // reactivate a previously unfollowed (soft-deleted) row instead of adding a second
        var alt = await db.Watchlists.IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.AgentId == agentId && w.EntityType == entityType && w.EntityId == entityId
                                      && w.IsDeleted, cancellationToken);
        if (alt is not null)
        {
            alt.IsDeleted = false;
            alt.DeletedAt = null;
            alt.DeletedById = null;
        }
        else
        {
            db.Watchlists.Add(new WatchlistEntry
            {
                AgentId = agentId,
                EntityType = entityType,
                EntityId = entityId,
            });
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UnfollowAsync(string entityType, string entityId, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        var agentId = actor.GetAgentId();
        if (string.IsNullOrWhiteSpace(agentId))
        {
            return;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entry = await db.Watchlists
            .FirstOrDefaultAsync(w => w.AgentId == agentId && w.EntityType == entityType && w.EntityId == entityId,
                cancellationToken);
        if (entry is null)
        {
            return;
        }
        db.Watchlists.Remove(entry);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsFollowedAsync(string entityType, string entityId, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        var agentId = actor.GetAgentId();
        if (string.IsNullOrWhiteSpace(agentId))
        {
            return false;
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Watchlists
            .AnyAsync(w => w.AgentId == agentId && w.EntityType == entityType && w.EntityId == entityId,
                cancellationToken);
    }

    public async Task<List<WatchlistEntry>> GetFollowedAsync(ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        var agentId = actor.GetAgentId();
        if (string.IsNullOrWhiteSpace(agentId))
        {
            return new();
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Watchlists
            .Where(w => w.AgentId == agentId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<FollowedRecord>> GetFollowedResolvedAsync(ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        var agentId = actor.GetAgentId();
        if (string.IsNullOrWhiteSpace(agentId))
        {
            return new();
        }
        // only-reader supervision may view followed classified records
        var isLeadership = actor.MayClassifiedRead();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entries = await db.Watchlists
            .Where(w => w.AgentId == agentId)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new { w.EntityType, w.EntityId, w.CreatedAt })
            .ToListAsync(cancellationToken);
        if (entries.Count == 0)
        {
            return new();
        }

        var refs = entries.Select(e => (e.EntityType, e.EntityId)).Distinct().ToList();
        var resolved = await RecordsReference.ResolveAsync(db, refs, cancellationToken,
            mayAllTaskforces: actor.MayAllTaskforcesSee(), meId: agentId);

        var result = new List<FollowedRecord>(entries.Count);
        foreach (var e in entries)
        {
            // in-memory visibility from the already-loaded classified flag; unresolved = trashed/unknown; agent files are leadership-only
            bool accessible;
            if (resolved.TryGetValue((e.EntityType, e.EntityId), out var a))
            {
                accessible = e.EntityType == nameof(Agent) ? isLeadership : (!a.Classified || isLeadership);
            }
            else
            {
                accessible = false;
            }
            result.Add(new FollowedRecord(
                e.EntityType, e.EntityId,
                accessible ? a.Display : "(nicht mehr zugänglich)",
                accessible ? a.Href : null,
                e.CreatedAt,
                accessible));
        }
        return result;
    }

    public async Task<List<string>> GetFollowerIdsAsync(string entityType, string entityId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Watchlists
            .Where(w => w.EntityType == entityType && w.EntityId == entityId)
            .Select(w => w.AgentId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
