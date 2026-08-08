using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NOOSE_Website.Data;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Statistics;
using NOOSE_Website.Models.Threat;

namespace NOOSE_Website.Services.Statistics;

/// <inheritdoc cref="IThreatStatisticsService" />
public class ThreatStatisticsService(
    IDbContextFactory<AppDbContext> dbFactory,
    IMemoryCache cache,
    ILogger<ThreatStatisticsService> logger) : IThreatStatisticsService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private const int BucketSize = 10;

    public async Task<ChartGrid> GetScoreHistogramAsync(StatisticsScope scope,
        CancellationToken cancellationToken = default)
        => await cache.GetOrCreateAsync($"stats:threat:histogram:v1:{scope.CacheToken}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

            // scores are small ints; pulling only the column keeps this cheap even at scale
            var personScores = await db.People
                .Where(p => (scope.IncludeClassified || !p.IsClassified) && p.ThreatScore != null && p.ThreatScore > 0)
                .Select(p => p.ThreatScore!.Value)
                .ToListAsync(cancellationToken);
            var factionScores = await db.Factions
                .Where(f => (scope.IncludeClassified || !f.IsClassified) && f.ThreatScore != null && f.ThreatScore > 0)
                .Select(f => f.ThreatScore!.Value)
                .ToListAsync(cancellationToken);

            var labels = ScoreBuckets().Select(b => b.Label).ToList();
            return new ChartGrid(labels,
            [
                new ChartSeriesData("Personen", Bucketise(personScores)),
                new ChartSeriesData("Fraktionen", Bucketise(factionScores)),
            ]);
        }) ?? ChartGrid.Empty;

    public async Task<ChartMatrix> GetBandOverTimeAsync(StatisticsScope scope, string entityType,
        CancellationToken cancellationToken = default)
        => await cache.GetOrCreateAsync($"stats:threat:bands:v1:{entityType}:{scope.CacheToken}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

            var now = DateTime.UtcNow;
            var start = scope.StartUtc(now);
            var visibleIds = await VisibleIdsAsync(db, scope, entityType, cancellationToken);
            if (visibleIds.Count == 0)
            {
                return ChartMatrix.Empty;
            }

            // ThreatScoreHistory carries no authorization of its own, so the parent's classification is
            // enforced here by restricting to ids the viewer may see
            var rows = await db.ThreatScoreHistory
                .Where(h => h.EntityType == entityType && h.Timestamp >= start
                    && h.Score != null && visibleIds.Contains(h.EntityId))
                .Select(h => new { h.EntityId, h.Timestamp, Score = h.Score!.Value })
                .ToListAsync(cancellationToken);

            var buckets = StatisticsBuckets.Starts(scope, now);
            var levels = HazardLevelLogic.All;
            var cells = levels.Select(_ => new int[buckets.Count]).ToList();

            // one reading per record per bucket: the last score inside the bucket
            var perBucket = rows
                .GroupBy(r => StatisticsBuckets.IndexOf(buckets, r.Timestamp))
                .Where(g => g.Key >= 0 && g.Key < buckets.Count);
            foreach (var group in perBucket)
            {
                foreach (var latest in group.GroupBy(r => r.EntityId)
                    .Select(g => g.OrderByDescending(r => r.Timestamp).First()))
                {
                    var level = HazardLevelLogic.From(latest.Score);
                    cells[(int)level][group.Key]++;
                }
            }

            var max = cells.SelectMany(c => c).DefaultIfEmpty(0).Max();
            return new ChartMatrix(
                levels.Select(HazardLevelLogic.Name).ToList(),
                buckets.Select(b => StatisticsBuckets.Label(b, scope)).ToList(),
                cells.Select(c => (IReadOnlyList<int>)c).ToList(),
                max);
        }) ?? ChartMatrix.Empty;

    public async Task<ChartGrid> GetComponentProfilesAsync(StatisticsScope scope, string entityType, int topN = 3,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var raw = entityType == nameof(Data.Entities.People.Person)
            ? await db.People
                .Where(p => (scope.IncludeClassified || !p.IsClassified)
                    && p.ThreatScore != null && p.ThreatScore > 0 && p.ThreatDetailJson != null)
                .OrderByDescending(p => p.ThreatScore)
                .ThenBy(p => p.Name)
                .Take(topN)
                .Select(p => new { p.Name, Json = p.ThreatDetailJson! })
                .ToListAsync(cancellationToken)
            : await db.Factions
                .Where(f => (scope.IncludeClassified || !f.IsClassified)
                    && f.ThreatScore != null && f.ThreatScore > 0 && f.ThreatDetailJson != null)
                .OrderByDescending(f => f.ThreatScore)
                .ThenBy(f => f.Name)
                .Take(topN)
                .Select(f => new { f.Name, Json = f.ThreatDetailJson! })
                .ToListAsync(cancellationToken);

        var parsed = raw
            .Select(r => (r.Name, Detail: Parse(r.Json)))
            .Where(r => r.Detail is { PartialScores.Count: > 0 })
            .ToList();
        if (parsed.Count == 0)
        {
            return ChartGrid.Empty;
        }

        // the first record defines the axis order; later ones are matched by component name
        var axes = parsed[0].Detail!.PartialScores.Select(p => p.Name).ToList();
        var series = parsed
            .Select(r => new ChartSeriesData(r.Name, axes
                .Select(a => r.Detail!.PartialScores.FirstOrDefault(p => p.Name == a)?.Points ?? 0)
                .ToList()))
            .ToList();
        return new ChartGrid(axes, series);
    }

    public async Task<IReadOnlyList<(string Name, IReadOnlyList<ChartPoint> Points)>> GetScoreVsConfidenceAsync(
        StatisticsScope scope, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var people = await db.People
            .Where(p => (scope.IncludeClassified || !p.IsClassified)
                && p.ThreatScore != null && p.ThreatScore > 0 && p.ThreatConfidence != null)
            .Select(p => new { p.Id, p.Name, Score = p.ThreatScore!.Value, Confidence = p.ThreatConfidence!.Value })
            .ToListAsync(cancellationToken);
        var factions = await db.Factions
            .Where(f => (scope.IncludeClassified || !f.IsClassified)
                && f.ThreatScore != null && f.ThreatScore > 0 && f.ThreatConfidence != null)
            .Select(f => new { f.Id, f.Name, Score = f.ThreatScore!.Value, Confidence = f.ThreatConfidence!.Value })
            .ToListAsync(cancellationToken);

        return
        [
            ("Personen", people
                .Select(p => new ChartPoint(p.Confidence, p.Score, p.Name, $"/personen/{p.Id}"))
                .ToList()),
            ("Fraktionen", factions
                .Select(f => new ChartPoint(f.Confidence, f.Score, f.Name, $"/fraktionen/{f.Id}"))
                .ToList()),
        ];
    }

    public async Task<ThreatHeadline> GetHeadlineAsync(StatisticsScope scope,
        CancellationToken cancellationToken = default)
        => await cache.GetOrCreateAsync($"stats:threat:headline:v1:{scope.CacheToken}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

            // two bounded queries rather than a UNION: Pomelo would have to reconcile two tables here
            var scores = await db.People
                .Where(p => (scope.IncludeClassified || !p.IsClassified) && p.ThreatScore != null && p.ThreatScore > 0)
                .Select(p => new { Score = p.ThreatScore!.Value, p.ThreatConfidence })
                .ToListAsync(cancellationToken);
            scores.AddRange(await db.Factions
                .Where(f => (scope.IncludeClassified || !f.IsClassified) && f.ThreatScore != null && f.ThreatScore > 0)
                .Select(f => new { Score = f.ThreatScore!.Value, f.ThreatConfidence })
                .ToListAsync(cancellationToken));

            if (scores.Count == 0)
            {
                return new ThreatHeadline(0, 0, 0, 0, 0);
            }
            return new ThreatHeadline(
                scores.Count,
                scores.Count(s => s.Score >= 50),
                scores.Count(s => s.Score >= 75),
                Math.Round(scores.Average(s => (double)s.Score), 1),
                Math.Round(scores.Where(s => s.ThreatConfidence != null)
                    .Select(s => (double)s.ThreatConfidence!.Value)
                    .DefaultIfEmpty(0).Average(), 1));
        }) ?? new ThreatHeadline(0, 0, 0, 0, 0);

    /// <summary>Ids of the parent records the viewer may see; the classification gate for the history table.</summary>
    private static async Task<List<string>> VisibleIdsAsync(AppDbContext db, StatisticsScope scope,
        string entityType, CancellationToken cancellationToken)
        => entityType == nameof(Data.Entities.People.Person)
            ? await db.People.Where(p => scope.IncludeClassified || !p.IsClassified)
                .Select(p => p.Id).ToListAsync(cancellationToken)
            : await db.Factions.Where(f => scope.IncludeClassified || !f.IsClassified)
                .Select(f => f.Id).ToListAsync(cancellationToken);

    private ThreatScoreDetail? Parse(string json)
    {
        try
        {
            // same options the score service writes with, so the breakdown round-trips
            return JsonSerializer.Deserialize<ThreatScoreDetail>(json, ThreatScoreService.JsonOptions);
        }
        catch (JsonException ex)
        {
            // a record with an unreadable breakdown is skipped, never fatal for the whole chart
            logger.LogWarning(ex, "Bedrohungs-Detail konnte nicht gelesen werden.");
            return null;
        }
    }

    private static IReadOnlyList<(string Label, int From, int Until)> ScoreBuckets()
    {
        var list = new List<(string, int, int)>();
        for (var from = 0; from < 100; from += BucketSize)
        {
            var until = from + BucketSize;
            list.Add(($"{from}–{until - 1}", from, until));
        }
        // the top bucket is closed so a score of exactly 100 is counted
        list[^1] = ("90–100", 90, 101);
        return list;
    }

    private static IReadOnlyList<double> Bucketise(IReadOnlyList<int> scores)
    {
        var buckets = ScoreBuckets();
        var counts = new double[buckets.Count];
        foreach (var score in scores)
        {
            for (var i = 0; i < buckets.Count; i++)
            {
                if (score >= buckets[i].From && score < buckets[i].Until)
                {
                    counts[i]++;
                    break;
                }
            }
        }
        return counts;
    }

}
