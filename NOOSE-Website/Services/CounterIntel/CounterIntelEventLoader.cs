using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Cases;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.Groups;
using NOOSE_Website.Data.Entities.Operations;
using NOOSE_Website.Data.Entities.Parties;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Models.CounterIntel;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>
/// Reads the access and audit logs once per evaluation and enriches every row with the actor and target
/// properties the rules may filter on. One pass for all rules — never a query per rule.
/// </summary>
public static class CounterIntelEventLoader
{
    /// <summary>Row ceiling per log table, so a busy month cannot blow up the page.</summary>
    public const int MaxRowsPerSource = 30000;

    // MySQL chokes on very long IN lists; enrichment ids go in batches
    private const int IdBatchSize = 500;

    /// <summary>Actor and target metadata resolved for the given definitions; timestamps are local.</summary>
    public static async Task<List<CounterIntelEvent>> LoadAsync(
        AppDbContext db, IReadOnlyList<CounterIntelRuleDefinition> definitions, CancellationToken cancellationToken = default)
    {
        if (definitions.Count == 0)
        {
            return [];
        }

        var windowDays = definitions
            .Select(d => Math.Clamp(d.WindowDays, 1, CounterIntelRuleDefinition.MaxWindowDays))
            .Max();
        var since = DateTime.UtcNow.AddDays(-windowDays);

        var wantsRead = definitions.Any(d => d.Actions.Count == 0 || d.Actions.Contains(CounterIntelActionKind.Read));
        var wantsWrites = definitions.Any(d => d.Actions.Count == 0 || d.Actions.Any(a => a != CounterIntelActionKind.Read));

        var actors = await ActorsAsync(db, cancellationToken);
        var rows = new List<Raw>();

        if (wantsRead)
        {
            var reads = await db.AccessLogs.AsNoTracking()
                .Where(a => a.AgentId != null && a.Timestamp >= since)
                .OrderByDescending(a => a.Timestamp)
                .Take(MaxRowsPerSource)
                .Select(a => new { a.AgentId, a.AgentName, a.Timestamp, a.EntityType, a.EntityId })
                .ToListAsync(cancellationToken);
            rows.AddRange(reads.Select(r => new Raw(
                r.AgentId!, r.AgentName, r.Timestamp, r.EntityType, r.EntityId, CounterIntelActionKind.Read)));
        }

        if (wantsWrites)
        {
            var writes = await db.AuditLogs.AsNoTracking()
                .Where(a => a.AgentId != null && a.Timestamp >= since)
                .OrderByDescending(a => a.Timestamp)
                .Take(MaxRowsPerSource)
                .Select(a => new { a.AgentId, a.AgentName, a.Timestamp, a.EntityType, a.EntityId, a.Action })
                .ToListAsync(cancellationToken);
            rows.AddRange(writes.Select(r => new Raw(
                r.AgentId!, r.AgentName, r.Timestamp, r.EntityType, r.EntityId,
                CounterIntelActionKindDisplay.From(r.Action))));
        }

        // read-only supervisors are invisible RP-wide and never subjects of counter-intelligence
        rows = rows.Where(r => !actors.TryGetValue(r.AgentId, out var a) || !a.IsOnlyReader).ToList();
        if (rows.Count == 0)
        {
            return [];
        }

        var targets = definitions.Any(d => d.NeedsTargetLookup)
            ? await TargetsAsync(db, rows, cancellationToken)
            : [];
        var tags = definitions.Any(d => d.NeedsTagLookup)
            ? await TagsAsync(db, rows, cancellationToken)
            : [];

        return rows.Select(r =>
        {
            var key = $"{r.EntityType}:{r.EntityId}";
            actors.TryGetValue(r.AgentId, out var actor);
            var target = targets.TryGetValue(key, out var t) ? t : (Target?)null;
            return new CounterIntelEvent
            {
                AgentId = r.AgentId,
                AgentName = string.IsNullOrWhiteSpace(actor.Codename) ? r.AgentName : actor.Codename,
                LocalTimestamp = r.TimestampUtc.ToLocalTime(),
                EntityType = r.EntityType,
                EntityId = r.EntityId,
                Action = r.Action,
                TargetIsClassified = target?.IsClassified,
                TargetClassification = target?.Level,
                TargetTagIds = tags.TryGetValue(key, out var ids) ? ids : null,
                ActorRank = actor.Rank,
                ActorIsTru = actor.IsTru,
                ActorIsHrb = actor.IsHrb,
                ActorIsAdmin = actor.IsAdmin,
                ActorPartnerAgency = actor.PartnerAgency,
            };
        }).ToList();
    }

    private static async Task<Dictionary<string, Actor>> ActorsAsync(AppDbContext db, CancellationToken ct)
    {
        var rows = await db.Users.AsNoTracking()
            .Select(u => new
            {
                u.Id, u.Codename, u.Rank, u.IsTRU, u.IsHRB, u.IsAdmin, u.IsTeamLead, u.PartnerAgency,
            })
            .ToListAsync(ct);
        return rows.ToDictionary(
            u => u.Id,
            u => new Actor(u.Codename, u.Rank, u.IsTRU, u.IsHRB, u.IsAdmin, u.IsTeamLead && !u.IsAdmin, u.PartnerAgency));
    }

    // soft-deleted targets stay resolvable: a rule on "Gelöscht" is exactly about records that are gone now
    private static async Task<Dictionary<string, Target>> TargetsAsync(
        AppDbContext db, List<Raw> rows, CancellationToken ct)
    {
        var result = new Dictionary<string, Target>();
        foreach (var group in rows.GroupBy(r => r.EntityType))
        {
            var ids = group.Select(r => r.EntityId).Distinct().ToList();
            foreach (var batch in Batches(ids))
            {
                var found = await FetchAsync(db, group.Key, batch, ct);
                foreach (var (id, target) in found)
                {
                    result[$"{group.Key}:{id}"] = target;
                }
            }
        }
        return result;
    }

    // flat "WHERE Id IN (…)" per type — Pomelo cannot translate CROSS APPLY / LATERAL on MySQL
    private static async Task<List<(string Id, Target Target)>> FetchAsync(
        AppDbContext db, string entityType, List<string> ids, CancellationToken ct) => entityType switch
    {
        nameof(Person) => Map(await db.People.AsNoTracking().IgnoreQueryFilters()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new Flat(x.Id, x.IsClassified, x.Classification)).ToListAsync(ct)),
        nameof(Faction) => Map(await db.Factions.AsNoTracking().IgnoreQueryFilters()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new Flat(x.Id, x.IsClassified, x.Classification)).ToListAsync(ct)),
        nameof(PersonGroup) => Map(await db.PersonGroups.AsNoTracking().IgnoreQueryFilters()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new Flat(x.Id, x.IsClassified, x.Classification)).ToListAsync(ct)),
        nameof(Party) => Map(await db.Parties.AsNoTracking().IgnoreQueryFilters()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new Flat(x.Id, x.IsClassified, x.Classification)).ToListAsync(ct)),
        nameof(Operation) => Map(await db.Operations.AsNoTracking().IgnoreQueryFilters()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new Flat(x.Id, x.IsClassified, x.Classification)).ToListAsync(ct)),
        nameof(Case) => Map(await db.Cases.AsNoTracking().IgnoreQueryFilters()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new Flat(x.Id, x.IsClassified, x.Classification)).ToListAsync(ct)),
        // taskforces carry no classification of their own
        nameof(Taskforce) => Map(await db.Taskforces.AsNoTracking().IgnoreQueryFilters()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new Flat(x.Id, x.IsClassified, Classification.Unknown)).ToListAsync(ct)),
        _ => [],
    };

    private static async Task<Dictionary<string, HashSet<string>>> TagsAsync(
        AppDbContext db, List<Raw> rows, CancellationToken ct)
    {
        var result = new Dictionary<string, HashSet<string>>();
        foreach (var group in rows.GroupBy(r => r.EntityType))
        {
            var ids = group.Select(r => r.EntityId).Distinct().ToList();
            foreach (var batch in Batches(ids))
            {
                var mappings = await db.TagMappings.AsNoTracking()
                    .Where(m => m.EntityType == group.Key && batch.Contains(m.EntityId))
                    .Select(m => new { m.EntityId, m.TagId })
                    .ToListAsync(ct);
                foreach (var m in mappings)
                {
                    var key = $"{group.Key}:{m.EntityId}";
                    if (!result.TryGetValue(key, out var set))
                    {
                        result[key] = set = [];
                    }
                    set.Add(m.TagId);
                }
            }
        }
        return result;
    }

    private static List<(string Id, Target Target)> Map(List<Flat> rows)
        => rows.Select(r => (r.Id, new Target(r.IsClassified, r.Level))).ToList();

    private static IEnumerable<List<string>> Batches(List<string> ids)
    {
        for (var i = 0; i < ids.Count; i += IdBatchSize)
        {
            yield return ids.GetRange(i, Math.Min(IdBatchSize, ids.Count - i));
        }
    }

    private sealed record Raw(
        string AgentId, string? AgentName, DateTime TimestampUtc, string EntityType, string EntityId,
        CounterIntelActionKind Action);

    private readonly record struct Actor(
        string? Codename, Rank? Rank, bool IsTru, bool IsHrb, bool IsAdmin, bool IsOnlyReader, PartnerAgency? PartnerAgency);

    private readonly record struct Target(bool IsClassified, Classification Level);

    // EF needs a named projection type it can translate; a tuple would not compose in the switch
    private sealed record Flat(string Id, bool IsClassified, Classification Level);
}
