using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NOOSE_Website.Data;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Statistics;

namespace NOOSE_Website.Services.Statistics;

/// <inheritdoc cref="IThroughputStatisticsService" />
public class ThroughputStatisticsService(
    IDbContextFactory<AppDbContext> dbFactory,
    IMemoryCache cache) : IThroughputStatisticsService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    /// <summary>Upper edge of each cycle-time bucket in days; the last bucket is open-ended.</summary>
    private static readonly int[] CycleEdges = [1, 3, 7, 14, 30, 60, 90];

    public async Task<ChartGrid> GetCaptureVersusMeasuresAsync(StatisticsScope scope,
        CancellationToken cancellationToken = default)
        => await cache.GetOrCreateAsync($"stats:throughput:capture:v1:{scope.CacheToken}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

            var now = DateTime.UtcNow;
            var start = scope.StartUtc(now);
            var buckets = StatisticsBuckets.Starts(scope, now);

            var newPeople = await db.People
                .Where(p => (scope.IncludeClassified || !p.IsClassified) && p.CreatedAt >= start)
                .Select(p => p.CreatedAt)
                .ToListAsync(cancellationToken);
            var measures = await db.PersonDocs
                .Where(d => (scope.IncludeClassified || !d.Person!.IsClassified) && d.Timestamp >= start)
                .Select(d => d.Timestamp)
                .ToListAsync(cancellationToken);

            return new ChartGrid(
                buckets.Select(b => StatisticsBuckets.Label(b, scope)).ToList(),
                [
                    new ChartSeriesData("Neue Personenakten", StatisticsBuckets.Count(newPeople, buckets)),
                    new ChartSeriesData("Erfasste Maßnahmen", StatisticsBuckets.Count(measures, buckets)),
                ]);
        }) ?? ChartGrid.Empty;

    public async Task<IReadOnlyList<ChartBucket>> GetCaseCycleTimeAsync(StatisticsScope scope,
        CancellationToken cancellationToken = default)
        => await cache.GetOrCreateAsync($"stats:throughput:cycle:v1:{scope.CacheToken}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

            var start = scope.StartUtc(DateTime.UtcNow);

            // only completed cases have a cycle time at all
            var spans = await db.Cases
                .Where(c => (scope.IncludeClassified || !c.IsClassified)
                    && c.CompletedAt != null && c.CompletedAt >= start)
                .Select(c => new { c.CreatedAt, Completed = c.CompletedAt!.Value })
                .ToListAsync(cancellationToken);

            var counts = new int[CycleEdges.Length + 1];
            foreach (var span in spans)
            {
                // a completion stamped before creation is data noise, not a negative duration
                var days = Math.Max(0, (span.Completed - span.CreatedAt).TotalDays);
                var index = Array.FindIndex(CycleEdges, edge => days <= edge);
                counts[index < 0 ? CycleEdges.Length : index]++;
            }

            var list = new List<ChartBucket>(counts.Length);
            var from = 0d;
            for (var i = 0; i < CycleEdges.Length; i++)
            {
                list.Add(new ChartBucket(Label(from, CycleEdges[i]), from, CycleEdges[i], counts[i]));
                from = CycleEdges[i];
            }
            list.Add(new ChartBucket($"> {CycleEdges[^1]} T", from, double.PositiveInfinity, counts[^1]));
            return (IReadOnlyList<ChartBucket>)list;
        }) ?? [];

    public async Task<ChartGrid> GetOpenedVersusClosedAsync(StatisticsScope scope,
        CancellationToken cancellationToken = default)
        => await cache.GetOrCreateAsync($"stats:throughput:flow:v1:{scope.CacheToken}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

            var now = DateTime.UtcNow;
            var start = scope.StartUtc(now);
            var buckets = StatisticsBuckets.Starts(scope, now);

            var opened = await db.Cases
                .Where(c => (scope.IncludeClassified || !c.IsClassified) && c.CreatedAt >= start)
                .Select(c => c.CreatedAt)
                .ToListAsync(cancellationToken);
            var closed = await db.Cases
                .Where(c => (scope.IncludeClassified || !c.IsClassified)
                    && c.CompletedAt != null && c.CompletedAt >= start)
                .Select(c => c.CompletedAt!.Value)
                .ToListAsync(cancellationToken);

            return new ChartGrid(
                buckets.Select(b => StatisticsBuckets.Label(b, scope)).ToList(),
                [
                    new ChartSeriesData("Eröffnet", StatisticsBuckets.Count(opened, buckets)),
                    new ChartSeriesData("Abgeschlossen", StatisticsBuckets.Count(closed, buckets)),
                ]);
        }) ?? ChartGrid.Empty;

    public async Task<IReadOnlyList<ChartRatio>> GetFollowupPunctualityAsync(StatisticsScope scope,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var start = scope.StartUtc(now);

        var rows = await db.Followups
            .Where(f => f.DueAt >= start)
            .Select(f => new { f.DueAt, f.Done, f.DoneAt })
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return [];
        }

        var done = rows.Where(r => r.Done).ToList();
        var onTime = done.Count(r => r.DoneAt is { } at && at <= r.DueAt);
        var openOverdue = rows.Count(r => !r.Done && r.DueAt < now);

        return
        [
            new ChartRatio("Pünktlich erledigt", onTime, done.Count, null),
            new ChartRatio("Erledigt insgesamt", done.Count, rows.Count, null),
            // a high share here is bad, so the meter's scale is inverted at the call site
            new ChartRatio("Offen und überfällig", openOverdue, rows.Count, null),
        ];
    }

    private static string Label(double from, int until)
        => from == 0 ? $"≤ {until} T" : $"{from:0}–{until} T";
}
