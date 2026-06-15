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

        // Nur folgen, was der Aufrufer auch sehen darf (Verschlusssache/Papierkorb/Personalakte-Gate serverseitig).
        if (!await Visibility.IsRecordVisibleAsync(db, entityType, entityId, actor.IsLeadership(), cancellationToken))
        {
            throw new UnauthorizedAccessException("Diese Akte ist für dich nicht zugänglich.");
        }

        // Bereits aktiv gefolgt → nichts zu tun.
        var active = await db.Watchlists
            .FirstOrDefaultAsync(w => w.AgentId == agentId && w.EntityType == entityType && w.EntityId == entityId,
                cancellationToken);
        if (active is not null)
        {
            return;
        }

        // Eine früher entfolgte (soft-gelöschte) Zeile reaktivieren statt eine zweite anzulegen.
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
        // Hard-Delete → der Audit-Interceptor wandelt es in einen Soft-Delete um.
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
        // Lese-Gate: die Nur-Lese-Aufsicht darf gefolgte VS-Akten einsehen (DarfVerschlusssacheLesen).
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
        // Gefolgte Taskforces nur auflösen, wenn der Aufrufer zugeteilt ist (oder alle sehen darf).
        var resolved = await RecordsReference.ResolveAsync(db, refs, cancellationToken,
            mayAllTaskforces: actor.MayAllTaskforcesSee(), meId: agentId);

        var result = new List<FollowedRecord>(entries.Count);
        foreach (var e in entries)
        {
            // Sichtbarkeit ohne zusätzliche DB-Abfrage je Eintrag: AktenReferenz hat das Verschlusssache-Flag bereits
            // mitgeladen (nicht auflösbar = Papierkorb/unbekannt → nicht zugänglich). Personalakten (Agent) gelten als
            // Führungs-Inhalt (entspricht Sichtbarkeit.IstAkteSichtbarAsync). Spiegelt die Logik dort 1:1, nur in-memory.
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
