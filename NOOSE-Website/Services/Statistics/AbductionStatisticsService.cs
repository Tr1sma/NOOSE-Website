using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NOOSE_Website.Data;
using NOOSE_Website.Models.Dashboard;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Statistics;

namespace NOOSE_Website.Services.Statistics;

/// <inheritdoc cref="IAbductionStatisticsService" />
public class AbductionStatisticsService(
    IDbContextFactory<AppDbContext> dbFactory,
    IMemoryCache cache) : IAbductionStatisticsService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);
    private const int TopN = 8;

    // abductions carry no VS axis (internal, visible to all), so only the window matters — not IncludeClassified
    public async Task<AbductionStatistics> GetAsync(StatisticsScope scope, CancellationToken cancellationToken = default)
        => await cache.GetOrCreateAsync($"stats:abductions:v1:{scope.CacheToken}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

            var now = DateTime.UtcNow;
            var start = scope.StartUtc(now);
            var buckets = StatisticsBuckets.Starts(scope, now);

            var rows = await db.AgentAbductions
                .Where(a => a.Timestamp >= start)
                .Select(a => new { a.Id, a.Timestamp, a.InformationLeaked, a.Outcome, a.LeakSeverity, a.PerpetratorType, a.PerpetratorId })
                .ToListAsync(cancellationToken);

            var labels = buckets.Select(b => StatisticsBuckets.Label(b, scope)).ToList();
            var overTime = new ChartGrid(labels,
            [
                new ChartSeriesData("mit Informationsabfluss",
                    StatisticsBuckets.Count(rows.Where(r => r.InformationLeaked).Select(r => r.Timestamp).ToList(), buckets)),
                new ChartSeriesData("ohne Abfluss",
                    StatisticsBuckets.Count(rows.Where(r => !r.InformationLeaked).Select(r => r.Timestamp).ToList(), buckets)),
            ]);

            var outcomeCounts = rows.GroupBy(r => r.Outcome).ToDictionary(g => g.Key, g => g.Count());
            var outcomes = new ChartGrid(
                AbductionOutcomeDisplay.All.Select(AbductionOutcomeDisplay.Name).ToList(),
                [new ChartSeriesData("Entführungen", AbductionOutcomeDisplay.All
                    .Select(o => (double)outcomeCounts.GetValueOrDefault(o)).ToList())]);

            var severityCounts = rows.Where(r => r.InformationLeaked)
                .GroupBy(r => r.LeakSeverity).ToDictionary(g => g.Key, g => g.Count());
            var severity = LeakSeverityDisplay.All
                .Where(s => s != LeakSeverity.None)
                .Select(s => new DistributionSegment(LeakSeverityDisplay.Name(s), severityCounts.GetValueOrDefault(s)))
                .ToList();

            var top = rows
                .GroupBy(r => (r.PerpetratorType, r.PerpetratorId))
                .Select(g => new { g.Key.PerpetratorType, g.Key.PerpetratorId, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(TopN)
                .ToList();
            var refs = top.Select(t => (t.PerpetratorType, t.PerpetratorId)).ToList();
            var resolved = await RecordsReference.ResolveAsync(db, refs, cancellationToken);
            var perpLabels = top.Select(t =>
            {
                resolved.TryGetValue((t.PerpetratorType, t.PerpetratorId), out var r);
                return string.IsNullOrWhiteSpace(r.Display) ? "(gelöschte Akte)" : r.Display;
            }).ToList();
            var topPerpetrators = new ChartGrid(perpLabels,
                [new ChartSeriesData("Entführungen", top.Select(t => (double)t.Count).ToList())]);

            var ids = rows.Select(r => r.Id).ToList();
            var activeCompromised = ids.Count == 0
                ? 0
                : await db.AbductionCompromises
                    .CountAsync(c => ids.Contains(c.AbductionId) && c.Status == CompromiseStatus.Compromised, cancellationToken);

            return new AbductionStatistics(
                rows.Count, rows.Count(r => r.InformationLeaked), activeCompromised,
                overTime, outcomes, topPerpetrators, severity);
        }) ?? AbductionStatistics.Empty;
}
