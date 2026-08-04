using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.Leads;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Leads;

namespace NOOSE_Website.Services;

/// <summary>Proactive, algorithmic investigation leads (link-prediction, new conflicts, stale high-classification). Read-mostly.</summary>
public interface ILeadService
{
    Task<IReadOnlyList<LeadGroup>> GetFeedAsync(ClaimsPrincipal viewer, CancellationToken cancellationToken = default);
    Task IgnoreAsync(string leadKey, LeadKind kind, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task UndoIgnoreAsync(string leadKey, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="ILeadService" />
public class LeadService(IDbContextFactory<AppDbContext> dbFactory, IMemoryCache cache) : ILeadService
{
    private const string CacheKey = "leads:raw:v1";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private const int MaxPerKind = 20;
    private const int HubDegreeCap = 60; // skip super-hubs to bound O(deg^2)
    private const int StaleDays = 30;
    private const int ConflictWindowDays = 14;

    public async Task<IReadOnlyList<LeadGroup>> GetFeedAsync(ClaimsPrincipal viewer, CancellationToken cancellationToken = default)
    {
        var scope = ViewerScope.From(viewer);
        if (scope.IsPartner)
        {
            return Array.Empty<LeadGroup>();
        }
        var isLeadership = viewer.IsLeadership();

        var raw = await cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await ComputeRawAsync(cancellationToken);
        }) ?? new List<Lead>();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var dismissed = (await db.LeadDismissals.Select(d => d.LeadKey).ToListAsync(cancellationToken)).ToHashSet();

        var visible = raw.Where(l => !dismissed.Contains(l.Key) && (!l.Classified || isLeadership));

        return visible
            .GroupBy(l => l.Kind)
            .Select(g => new LeadGroup(g.Key, g.OrderByDescending(l => l.Score).ThenBy(l => l.Title).Take(MaxPerKind).ToList()))
            .OrderBy(g => (int)g.Kind)
            .ToList();
    }

    public async Task IgnoreAsync(string leadKey, LeadKind kind, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireWriteAccess(actor);
        if (string.IsNullOrWhiteSpace(leadKey))
        {
            return;
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.LeadDismissals.IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.LeadKey == leadKey, cancellationToken);
        if (existing is null)
        {
            db.LeadDismissals.Add(new LeadDismissal { LeadKey = leadKey, Kind = kind });
        }
        else
        {
            existing.IsDeleted = false;
            existing.DeletedAt = null;
            existing.DeletedById = null;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UndoIgnoreAsync(string leadKey, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireWriteAccess(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.LeadDismissals.FirstOrDefaultAsync(d => d.LeadKey == leadKey, cancellationToken);
        if (existing is null)
        {
            return;
        }
        existing.IsDeleted = true;
        existing.DeletedAt = DateTime.UtcNow;
        existing.DeletedById = actor.GetAgentId();
        await db.SaveChangesAsync(cancellationToken);
    }

    // viewer-independent candidate set (cached); VS + dismissal are applied per request
    private async Task<List<Lead>> ComputeRawAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var leads = new List<Lead>();
        await AddLinkPredictionAsync(db, leads, cancellationToken);
        await AddNewConflictAsync(db, leads, cancellationToken);
        await AddStaleHighClassificationAsync(db, leads, cancellationToken);
        return leads;
    }

    private static async Task AddLinkPredictionAsync(AppDbContext db, List<Lead> leads, CancellationToken ct)
    {
        var edges = await GraphEdgeLoader.LoadRawEdgesAsync(db, null, ct);
        var adj = new Dictionary<string, HashSet<string>>();
        void Link(string a, string b)
        {
            if (!adj.TryGetValue(a, out var set))
            {
                set = new HashSet<string>();
                adj[a] = set;
            }
            set.Add(b);
        }
        foreach (var e in edges)
        {
            if (e.Source == e.Target)
            {
                continue;
            }
            Link(e.Source, e.Target);
            Link(e.Target, e.Source);
        }

        const string personPrefix = nameof(Person) + ":";
        var pairCount = new Dictionary<(string A, string B), int>();
        foreach (var (_, neighbors) in adj)
        {
            if (neighbors.Count > HubDegreeCap)
            {
                continue; // hub — too many pairs, skip
            }
            var persons = neighbors.Where(n => n.StartsWith(personPrefix, StringComparison.Ordinal)).ToList();
            for (var i = 0; i < persons.Count; i++)
            {
                for (var j = i + 1; j < persons.Count; j++)
                {
                    var key = string.CompareOrdinal(persons[i], persons[j]) < 0
                        ? (persons[i], persons[j])
                        : (persons[j], persons[i]);
                    pairCount[key] = pairCount.GetValueOrDefault(key) + 1;
                }
            }
        }

        // qualifying pairs: >= 2 shared neighbours and not already directly linked
        var qualifying = pairCount
            .Where(kv => kv.Value >= 2 && !(adj.TryGetValue(kv.Key.A, out var an) && an.Contains(kv.Key.B)))
            .OrderByDescending(kv => kv.Value)
            .Take(40)
            .ToList();
        if (qualifying.Count == 0)
        {
            return;
        }

        var ids = qualifying.SelectMany(kv => new[] { Id(kv.Key.A), Id(kv.Key.B) }).Distinct().ToList();
        var people = await db.People.Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Name, p.IsClassified })
            .ToDictionaryAsync(p => p.Id, p => p, ct);

        foreach (var kv in qualifying)
        {
            var aid = Id(kv.Key.A);
            var bid = Id(kv.Key.B);
            if (!people.TryGetValue(aid, out var a) || !people.TryGetValue(bid, out var b))
            {
                continue;
            }
            leads.Add(new Lead(
                LeadKind.LinkPrediction,
                $"LP|{aid}|{bid}",
                "Mögliche Verbindung",
                $"{a.Name} & {b.Name} teilen {kv.Value} gemeinsame Kontakte, sind aber nicht verknüpft.",
                kv.Value,
                a.IsClassified || b.IsClassified,
                nameof(Person), aid, a.Name, $"/personen/{aid}",
                nameof(Person), bid, b.Name, $"/personen/{bid}"));
        }
    }

    private static async Task AddNewConflictAsync(AppDbContext db, List<Lead> leads, CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddDays(-ConflictWindowDays);
        var conflicts = await db.Links
            .Where(v => !v.Automatic && v.Kind == LinkKind.Conflict && v.CreatedAt >= since)
            .Select(v => new { v.Id, v.SourceType, v.SourceId, v.TargetType, v.TargetId, v.CreatedAt })
            .ToListAsync(ct);
        if (conflicts.Count == 0)
        {
            return;
        }

        var refs = conflicts
            .SelectMany(c => new[] { (c.SourceType, c.SourceId), (c.TargetType, c.TargetId) })
            .Distinct().ToList();
        var map = await RecordsReference.ResolveAsync(db, refs, ct);

        var now = DateTime.UtcNow;
        foreach (var c in conflicts)
        {
            if (!map.TryGetValue((c.SourceType, c.SourceId), out var src) || !map.TryGetValue((c.TargetType, c.TargetId), out var tgt))
            {
                continue;
            }
            var ageDays = Math.Max(0, (now - c.CreatedAt).TotalDays);
            leads.Add(new Lead(
                LeadKind.NewConflict,
                $"NC|{c.Id}",
                "Neuer Konflikt",
                $"{src.Display} ⚔ {tgt.Display}",
                (int)Math.Round(ConflictWindowDays - ageDays), // newer = higher
                src.Classified || tgt.Classified,
                c.SourceType, c.SourceId, src.Display, src.Href,
                c.TargetType, c.TargetId, tgt.Display, tgt.Href));
        }
    }

    private static async Task AddStaleHighClassificationAsync(AppDbContext db, List<Lead> leads, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var staleBefore = now.AddDays(-StaleDays);

        var people = await db.People.Where(p => p.Classification >= Classification.SuspicionCase)
            .Select(p => new { p.Id, p.Name, p.IsClassified, p.Classification, Touched = p.ModifiedAt ?? p.CreatedAt })
            .ToListAsync(ct);
        foreach (var p in people.Where(p => p.Touched < staleBefore))
        {
            var days = (int)Math.Max(1, (now - p.Touched).TotalDays);
            leads.Add(new Lead(
                LeadKind.StaleHighClassification,
                $"ST|{nameof(Person)}|{p.Id}",
                "Veraltete Einstufung",
                $"{p.Name} ({ClassificationDisplay.Name(p.Classification)}) – seit {days} Tagen nicht aktualisiert.",
                days,
                p.IsClassified,
                nameof(Person), p.Id, p.Name, $"/personen/{p.Id}"));
        }

        var factions = await db.Factions.Where(f => f.Classification >= Classification.SuspicionCase)
            .Select(f => new { f.Id, f.Name, f.IsClassified, f.Classification, Touched = f.ModifiedAt ?? f.CreatedAt })
            .ToListAsync(ct);
        foreach (var f in factions.Where(f => f.Touched < staleBefore))
        {
            var days = (int)Math.Max(1, (now - f.Touched).TotalDays);
            leads.Add(new Lead(
                LeadKind.StaleHighClassification,
                $"ST|{nameof(Faction)}|{f.Id}",
                "Veraltete Einstufung",
                $"{f.Name} ({ClassificationDisplay.Name(f.Classification)}) – seit {days} Tagen nicht aktualisiert.",
                days,
                f.IsClassified,
                nameof(Faction), f.Id, f.Name, $"/fraktionen/{f.Id}"));
        }
    }

    private static string Id(string nodeKey)
    {
        var i = nodeKey.IndexOf(':');
        return i > 0 ? nodeKey[(i + 1)..] : nodeKey;
    }
}
