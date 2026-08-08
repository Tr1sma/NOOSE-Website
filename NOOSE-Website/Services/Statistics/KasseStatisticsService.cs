using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NOOSE_Website.Data;
using NOOSE_Website.Models.Dashboard;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Statistics;

namespace NOOSE_Website.Services.Statistics;

/// <inheritdoc cref="IKasseStatisticsService" />
public class KasseStatisticsService(
    IDbContextFactory<AppDbContext> dbFactory,
    IMemoryCache cache,
    IKassenService kasse) : IKasseStatisticsService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);
    private const int TopN = 8;

    // treasury carries no VS axis, so only the window matters — key on the range only, not IncludeClassified
    public async Task<KasseStatistics> GetAsync(StatisticsScope scope, CancellationToken cancellationToken = default)
    {
        // windowed aggregates are cacheable; the whole-ledger balances are read live so the tiles match /kasse
        var windowed = await cache.GetOrCreateAsync($"stats:kasse:v1:{StatisticsRangeDisplay.Token(scope.Range)}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

            var now = DateTime.UtcNow;
            var start = scope.StartUtc(now);
            var buckets = StatisticsBuckets.Starts(scope, now);

            var rows = await db.KassenBuchungen
                .Where(b => b.Timestamp >= start)
                .Select(b => new { b.Timestamp, b.Kind, b.Amount, b.Reason })
                .ToListAsync(cancellationToken);

            var labels = buckets.Select(b => StatisticsBuckets.Label(b, scope)).ToList();
            var deposits = rows.Where(r => r.Kind == KassenBuchungArt.Einzahlung).Select(r => (r.Timestamp, r.Amount)).ToList();
            var withdrawals = rows.Where(r => r.Kind == KassenBuchungArt.Auszahlung).Select(r => (r.Timestamp, r.Amount)).ToList();

            var movements = new ChartGrid(labels,
            [
                new ChartSeriesData("Einzahlungen", SumInto(deposits, buckets)),
                new ChartSeriesData("Auszahlungen", SumInto(withdrawals, buckets)),
            ]);

            var topReasons = rows
                .Where(r => !string.IsNullOrWhiteSpace(r.Reason))
                .GroupBy(r => r.Reason!.Trim())
                .Select(g => new DistributionSegment(g.Key, g.Count()))
                .OrderByDescending(s => s.Count)
                .Take(TopN)
                .ToList();

            return new KasseStatistics(0m, 0m,
                deposits.Sum(d => d.Amount), withdrawals.Sum(w => w.Amount),
                movements, ChartGrid.Empty, topReasons);
        }) ?? KasseStatistics.Empty;

        // live current balances (whole ledger); never cached, so they can't lag the /kasse page
        var summaries = await kasse.GetSummariesAsync(cancellationToken);
        var schwarz = summaries.First(s => s.Account == KassenKonto.Schwarzgeld).Balance;
        var gruen = summaries.First(s => s.Account == KassenKonto.Gruengeld).Balance;
        var balances = new ChartGrid(
            KassenKontoDisplay.All.Select(KassenKontoDisplay.Name).ToList(),
            [new ChartSeriesData("Kontostand", [(double)schwarz, (double)gruen])]);

        return windowed with { SchwarzgeldBalance = schwarz, GruengeldBalance = gruen, Balances = balances };
    }

    /// <summary>Sums amounts into their buckets; anything before the first bucket is dropped.</summary>
    private static IReadOnlyList<double> SumInto(IReadOnlyList<(DateTime Timestamp, decimal Amount)> items, List<DateTime> buckets)
    {
        var sums = new double[buckets.Count];
        foreach (var (timestamp, amount) in items)
        {
            var index = StatisticsBuckets.IndexOf(buckets, timestamp);
            if (index >= 0)
            {
                sums[index] += (double)amount;
            }
        }
        return sums;
    }
}
