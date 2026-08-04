using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Cases;
using NOOSE_Website.Data.Entities.Gamification;
using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Gamification;

namespace NOOSE_Website.Services;

/// <summary>Read-only performance stats + milestone badges. Counts derive from IAuditable.CreatedById,
/// classification/observation authorship and audited case-status transitions; badges are awarded by a daily sweep.</summary>
public interface IGamificationService
{
    Task<AgentStats> GetStatsAsync(string agentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardAsync(GamificationPeriod period, int topN = 25, CancellationToken cancellationToken = default);
    /// <summary>Leaderboard over the last <paramref name="windowDays"/> days (0 or less = all time).</summary>
    Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardAsync(int windowDays, int topN = 25, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BadgeView>> GetBadgesAsync(string agentId, CancellationToken cancellationToken = default);
    /// <summary>Award any newly-earned badges across all agents; returns how many were granted. Idempotent.</summary>
    Task<int> SweepAsync(CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IGamificationService" />
public sealed class GamificationService(IDbContextFactory<AppDbContext> dbFactory) : IGamificationService
{
    public async Task<AgentStats> GetStatsAsync(string agentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(agentId))
        {
            return AgentStats.Empty;
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var all = await ComputeAllAsync(db, null, cancellationToken);
        var acc = all.GetValueOrDefault(agentId) ?? new StatAcc();
        var badges = await db.AgentBadges.CountAsync(b => b.AgentId == agentId, cancellationToken);
        return acc.ToStats(badges);
    }

    public Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardAsync(
        GamificationPeriod period, int topN = 25, CancellationToken cancellationToken = default)
        => BuildLeaderboardAsync(Cutoff(period), topN, cancellationToken);

    public Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardAsync(
        int windowDays, int topN = 25, CancellationToken cancellationToken = default)
        => BuildLeaderboardAsync(windowDays > 0 ? DateTime.UtcNow.AddDays(-windowDays) : null, topN, cancellationToken);

    private async Task<IReadOnlyList<LeaderboardEntry>> BuildLeaderboardAsync(
        DateTime? since, int topN, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var all = await ComputeAllAsync(db, since, cancellationToken);

        // ranking is over internal, active agents only (partners and inactive users excluded)
        var agents = await db.Users.Where(u => u.Status == AgentStatus.Active && u.PartnerAgency == null)
            .Select(u => new { u.Id, u.Codename })
            .ToListAsync(cancellationToken);

        return agents
            .Select(a => (a.Id, a.Codename, Acc: all.GetValueOrDefault(a.Id) ?? new StatAcc()))
            .Select(x => (x.Id, x.Codename, x.Acc, Stats: x.Acc.ToStats(0)))
            .Where(x => x.Stats.Points > 0)
            .OrderByDescending(x => x.Stats.Points).ThenBy(x => x.Codename, StringComparer.OrdinalIgnoreCase)
            .Take(topN)
            .Select((x, i) => new LeaderboardEntry(
                i + 1, x.Id, string.IsNullOrWhiteSpace(x.Codename) ? "(unbenannt)" : x.Codename,
                x.Stats.Points, x.Acc.Records, x.Acc.Docs, x.Acc.Links, x.Acc.SolvedCases))
            .ToList();
    }

    public async Task<IReadOnlyList<BadgeView>> GetBadgesAsync(string agentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(agentId))
        {
            return Array.Empty<BadgeView>();
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var awarded = await db.AgentBadges.Where(b => b.AgentId == agentId)
            .Select(b => new { b.BadgeKey, b.AwardedAt })
            .ToListAsync(cancellationToken);
        return awarded
            .Select(a => (Def: BadgeCatalog.Find(a.BadgeKey), a.AwardedAt))
            .Where(x => x.Def is not null)
            .OrderByDescending(x => x.AwardedAt)
            .Select(x => new BadgeView(x.Def!.Key, x.Def.Label, x.Def.Icon, x.Def.Description, x.AwardedAt))
            .ToList();
    }

    public async Task<int> SweepAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var all = await ComputeAllAsync(db, null, cancellationToken);
        var existing = (await db.AgentBadges.Select(b => new { b.AgentId, b.BadgeKey }).ToListAsync(cancellationToken))
            .Select(x => (x.AgentId, x.BadgeKey)).ToHashSet();

        var now = DateTime.UtcNow;
        var added = 0;
        foreach (var (agentId, acc) in all)
        {
            var stats = acc.ToStats(0);
            foreach (var def in BadgeCatalog.All)
            {
                // unique (AgentId, BadgeKey) index is the hard guard; the set avoids redundant inserts on overlapping sweeps
                if (def.Earned(stats) && existing.Add((agentId, def.Key)))
                {
                    db.AgentBadges.Add(new AgentBadge { AgentId = agentId, BadgeKey = def.Key, AwardedAt = now });
                    added++;
                }
            }
        }
        if (added > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        return added;
    }

    private static DateTime? Cutoff(GamificationPeriod period) => period switch
    {
        GamificationPeriod.Week => DateTime.UtcNow.AddDays(-7),
        GamificationPeriod.Month => DateTime.UtcNow.AddDays(-30),
        _ => null,
    };

    // one grouped pass per contribution source, keyed by the crediting agent id
    private static async Task<Dictionary<string, StatAcc>> ComputeAllAsync(AppDbContext db, DateTime? since, CancellationToken ct)
    {
        var acc = new Dictionary<string, StatAcc>(StringComparer.Ordinal);
        StatAcc Get(string id)
        {
            if (!acc.TryGetValue(id, out var s))
            {
                s = new StatAcc();
                acc[id] = s;
            }
            return s;
        }

        async Task AddCreated<T>(IQueryable<T> set, Action<StatAcc, int> apply) where T : class, IAuditable
        {
            var rows = await set
                .Where(x => x.CreatedById != null && (since == null || x.CreatedAt >= since))
                .GroupBy(x => x.CreatedById!)
                .Select(g => new { Id = g.Key, Count = g.Count() })
                .ToListAsync(ct);
            foreach (var r in rows)
            {
                apply(Get(r.Id), r.Count);
            }
        }

        await AddCreated(db.People, (s, n) => s.Records += n);
        await AddCreated(db.Factions, (s, n) => s.Records += n);
        await AddCreated(db.PersonGroups, (s, n) => s.Records += n);
        await AddCreated(db.Parties, (s, n) => s.Records += n);
        await AddCreated(db.Operations, (s, n) => s.Records += n);
        await AddCreated(db.Cases, (s, n) => s.Records += n);
        await AddCreated(db.PersonDocs, (s, n) => s.Docs += n);
        await AddCreated(db.Links, (s, n) => s.Links += n);

        // classifications credited to the deciding agent
        foreach (var r in await db.ClassificationHistory
            .Where(h => h.AgentId != null && (since == null || h.Timestamp >= since))
            .GroupBy(h => h.AgentId!).Select(g => new { Id = g.Key, Count = g.Count() }).ToListAsync(ct))
        {
            Get(r.Id).Classifications += r.Count;
        }

        // observations credited to the observing agent
        foreach (var r in await db.Observations
            .Where(o => o.ObservingAgentId != null && o.ObservingAgentId != "" && (since == null || o.CreatedAt >= since))
            .GroupBy(o => o.ObservingAgentId!).Select(g => new { Id = g.Key, Count = g.Count() }).ToListAsync(ct))
        {
            Get(r.Id).Observations += r.Count;
        }

        // solved cases: audited status transitions to Completed
        foreach (var a in await db.AuditLogs
            .Where(x => x.AgentId != null && x.EntityType == nameof(Case) && x.Action == AuditAction.Modified
                && x.ChangesJson != null && x.ChangesJson.Contains("Status") && (since == null || x.Timestamp >= since))
            .Select(x => new { x.AgentId, x.ChangesJson })
            .ToListAsync(ct))
        {
            if (IsCompletedTransition(a.ChangesJson))
            {
                Get(a.AgentId!).SolvedCases++;
            }
        }

        return acc;
    }

    // ChangesJson is {"Status":[old,new]}; new is the enum value (number or name)
    private static bool IsCompletedTransition(string? changesJson)
    {
        if (string.IsNullOrEmpty(changesJson))
        {
            return false;
        }
        try
        {
            using var doc = JsonDocument.Parse(changesJson);
            if (!doc.RootElement.TryGetProperty("Status", out var el)
                || el.ValueKind != JsonValueKind.Array || el.GetArrayLength() < 2)
            {
                return false;
            }
            var next = el[1];
            return next.ValueKind switch
            {
                JsonValueKind.Number => next.TryGetInt32(out var v) && v == (int)CaseStatus.Completed,
                JsonValueKind.String => string.Equals(next.GetString(), nameof(CaseStatus.Completed), StringComparison.OrdinalIgnoreCase),
                _ => false,
            };
        }
        catch (JsonException)
        {
            return false; /* malformed change payload */
        }
    }

    private sealed class StatAcc
    {
        public int Records;
        public int Docs;
        public int Links;
        public int Classifications;
        public int Observations;
        public int SolvedCases;

        public AgentStats ToStats(int badges) => new(Records, Docs, Links, Classifications, Observations, SolvedCases, badges);
    }
}
