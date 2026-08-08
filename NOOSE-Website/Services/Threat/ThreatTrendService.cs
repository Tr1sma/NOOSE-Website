using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Models.Threat;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IThreatTrendService" />
public class ThreatTrendService(IDbContextFactory<AppDbContext> dbFactory) : IThreatTrendService
{
    public async Task<IReadOnlyList<ThreatScorePoint>> GetHistoryAsync(
        string entityType, string entityId, int days = 180, CancellationToken cancellationToken = default)
    {
        var since = DateTime.UtcNow.AddDays(-days);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.ThreatScoreHistory
            .Where(h => h.EntityType == entityType && h.EntityId == entityId && h.Timestamp >= since)
            .OrderBy(h => h.Timestamp)
            .Select(h => new { h.Timestamp, h.Score, h.Confidence })
            .ToListAsync(cancellationToken);
        return rows.Select(r => new ThreatScorePoint(r.Timestamp, r.Score, r.Confidence)).ToList();
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<int>>> GetSparklinesAsync(
        string entityType, IReadOnlyCollection<string> ids, int points = 8, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<string, IReadOnlyList<int>>();
        }
        var idSet = ids.ToHashSet();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.ThreatScoreHistory
            .Where(h => h.EntityType == entityType && idSet.Contains(h.EntityId) && h.Score != null)
            .OrderBy(h => h.Timestamp)
            .Select(h => new { h.EntityId, Score = h.Score!.Value })
            .ToListAsync(cancellationToken);
        return rows.GroupBy(r => r.EntityId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<int>)g.TakeLast(points).Select(x => x.Score).ToList());
    }

    public async Task<IReadOnlyList<ThreatRaceFrame>> GetFactionRaceAsync(
        ClaimsPrincipal actor, int months = 12, int topN = 12, CancellationToken cancellationToken = default)
    {
        var isLeadership = actor.IsLeadership();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var firstMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-(months - 1));

        var factions = await db.Factions
            .Where(f => isLeadership || !f.IsClassified)
            .Select(f => new { f.Id, f.Name, f.IsClassified })
            .ToListAsync(cancellationToken);
        if (factions.Count == 0)
        {
            return Array.Empty<ThreatRaceFrame>();
        }
        var ids = factions.Select(f => f.Id).ToHashSet();
        var meta = factions.ToDictionary(f => f.Id);

        var snaps = await db.ThreatScoreHistory
            .Where(h => h.EntityType == nameof(Faction) && h.Score != null)
            .Select(h => new { h.EntityId, Score = h.Score!.Value, h.Timestamp })
            .ToListAsync(cancellationToken);
        var byFaction = snaps.Where(s => ids.Contains(s.EntityId))
            .GroupBy(s => s.EntityId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Timestamp).ToList());

        var frames = new List<ThreatRaceFrame>(months);
        for (var i = 0; i < months; i++)
        {
            var month = firstMonth.AddMonths(i);
            var cutoff = month.AddMonths(1);
            var entries = new List<ThreatRaceEntry>();
            foreach (var (fid, list) in byFaction)
            {
                var asOf = list.LastOrDefault(x => x.Timestamp < cutoff);
                if (asOf is null)
                {
                    continue;
                }
                var m = meta[fid];
                entries.Add(new ThreatRaceEntry(fid, m.Name, asOf.Score, m.IsClassified));
            }
            var top = entries.OrderByDescending(e => e.Score).ThenBy(e => e.Name).Take(topN).ToList();
            frames.Add(new ThreatRaceFrame(month, top));
        }
        return frames;
    }

    public async Task<IReadOnlyList<ThreatMover>> GetTopMoversAsync(
        ClaimsPrincipal actor, int windowDays = 30, int topN = 10, CancellationToken cancellationToken = default)
    {
        var isLeadership = actor.IsLeadership();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var windowStart = DateTime.UtcNow.AddDays(-windowDays);

        var factionMeta = (await db.Factions.Where(f => isLeadership || !f.IsClassified)
            .Select(f => new { f.Id, f.Name, f.IsClassified }).ToListAsync(cancellationToken))
            .ToDictionary(f => f.Id, f => (f.Name, f.IsClassified));
        var personMeta = (await db.People.Where(p => isLeadership || !p.IsClassified)
            .Select(p => new { p.Id, p.Name, p.IsClassified }).ToListAsync(cancellationToken))
            .ToDictionary(p => p.Id, p => (p.Name, p.IsClassified));

        var movers = new List<ThreatMover>();
        await CollectMoversAsync(db, nameof(Faction), factionMeta, "/fraktionen/", windowStart, movers, cancellationToken);
        await CollectMoversAsync(db, nameof(Person), personMeta, "/personen/", windowStart, movers, cancellationToken);

        // rank by magnitude of movement (rises AND drops) to match the panel's "größte Score-Bewegungen"
        return movers.OrderByDescending(m => Math.Abs(m.Delta)).ThenByDescending(m => m.ToScore).Take(topN).ToList();
    }

    private async Task CollectMoversAsync(
        AppDbContext db, string entityType, Dictionary<string, (string Name, bool Classified)> meta,
        string hrefPrefix, DateTime windowStart, List<ThreatMover> movers, CancellationToken cancellationToken)
    {
        if (meta.Count == 0)
        {
            return;
        }
        var ids = meta.Keys.ToHashSet();
        var snaps = await db.ThreatScoreHistory
            .Where(h => h.EntityType == entityType && h.Score != null)
            .Select(h => new { h.EntityId, Score = h.Score!.Value, h.Timestamp })
            .ToListAsync(cancellationToken);
        foreach (var g in snaps.Where(s => ids.Contains(s.EntityId)).GroupBy(s => s.EntityId))
        {
            var ordered = g.OrderBy(x => x.Timestamp).ToList();
            var to = ordered[^1];
            // baseline: latest snapshot at/before the window start, else the earliest we have
            var from = ordered.LastOrDefault(x => x.Timestamp <= windowStart) ?? ordered[0];
            var delta = to.Score - from.Score;
            if (delta == 0)
            {
                continue;
            }
            var m = meta[g.Key];
            movers.Add(new ThreatMover(entityType, g.Key, m.Name, from.Score, to.Score, delta, m.Classified, $"{hrefPrefix}{g.Key}"));
        }
    }
}
