using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Models.Dashboard;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Statistics;

namespace NOOSE_Website.Services.Statistics;

/// <inheritdoc cref="IStatistikService" />
public class StatisticsService(IDbContextFactory<AppDbContext> dbFactory, IDashboardService dashboard) : IStatisticsService
{
    /// <summary>Time series month count.</summary>
    private const int TimeSeriesMonths = 12;

    public async Task<StatisticsReport> GetReportAsync(bool isLeadership, string? meId, int topN = 10,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // consistent snapshot time
        var now = DateTime.UtcNow;

        // reuse dashboard metrics
        var metrics = await dashboard.GetMetricsAsync(isLeadership, meId, cancellationToken);

        // 1) by classification
        var personClassification = (await db.People
                .Where(p => isLeadership || !p.IsClassified)
                .GroupBy(p => p.Classification)
                .Select(g => new { Value = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => x.Value, x => x.Count);
        var peopleByClassification = ClassificationDisplay.All
            .Select(e => new DistributionSegment(ClassificationDisplay.Name(e), personClassification.GetValueOrDefault(e)))
            .ToList();

        // 2) by hazard
        var peopleByHazard = await HazardDistributionAsync(
            db.People.Where(p => isLeadership || !p.IsClassified).Select(p => p.ThreatScore), cancellationToken);

        // 3) by life status
        var lifeRaw = await db.People
            .Where(p => isLeadership || !p.IsClassified)
            .Select(p => new { p.LifeStatus, p.DeadUntil })
            .ToListAsync(cancellationToken);
        var lifeCount = lifeRaw
            .GroupBy(x => LifeStatusLogic.Effective(x.LifeStatus, x.DeadUntil, now))
            .ToDictionary(g => g.Key, g => g.Count());
        var peopleByLifeStatus = LifeStatusDisplay.All
            .Select(s => new DistributionSegment(LifeStatusDisplay.Name(s), lifeCount.GetValueOrDefault(s)))
            .ToList();

        // 4) factions hazard
        var factionsByHazard = await HazardDistributionAsync(
            db.Factions.Where(f => isLeadership || !f.IsClassified).Select(f => f.ThreatScore), cancellationToken);

        // 5) measure outcomes
        var outcomeCount = (await db.PersonDocs
                .Where(d => isLeadership || !d.Person!.IsClassified)
                .GroupBy(d => d.Outcome)
                .Select(g => new { Value = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => x.Value, x => x.Count);
        var measureOutcomes = MeasureOutcomeDisplay.All
            .Select(a => new DistributionSegment(MeasureOutcomeDisplay.Name(a), outcomeCount.GetValueOrDefault(a)))
            .ToList();

        // 6) cases by status
        var statusCount = (await db.Cases
                .Where(v => isLeadership || !v.IsClassified)
                .GroupBy(v => v.Status)
                .Select(g => new { Value = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => x.Value, x => x.Count);
        var casesByStatus = CaseStatusDisplay.All
            .Select(s => new DistributionSegment(CaseStatusDisplay.Name(s), statusCount.GetValueOrDefault(s)))
            .ToList();

        // 7) top threats
        var topPeopleRaw = await db.People
            .Where(p => (isLeadership || !p.IsClassified) && p.ThreatScore != null && p.ThreatScore > 0)
            .OrderByDescending(p => p.ThreatScore)
            .ThenBy(p => p.Name)
            .Select(p => new { p.Id, p.Name, p.CaseNumber, p.ThreatScore })
            .Take(topN)
            .ToListAsync(cancellationToken);
        var topPeople = topPeopleRaw
            .Select(p => new StatisticsTopEntry(p.Name, p.CaseNumber, $"/personen/{p.Id}",
                p.ThreatScore ?? 0, HazardLevelLogic.From(p.ThreatScore)))
            .ToList();

        var topFactionsRaw = await db.Factions
            .Where(f => (isLeadership || !f.IsClassified) && f.ThreatScore != null && f.ThreatScore > 0)
            .OrderByDescending(f => f.ThreatScore)
            .ThenBy(f => f.Name)
            .Select(f => new { f.Id, f.Name, f.CaseNumber, f.ThreatScore })
            .Take(topN)
            .ToListAsync(cancellationToken);
        var topFactions = topFactionsRaw
            .Select(f => new StatisticsTopEntry(f.Name, f.CaseNumber, $"/fraktionen/{f.Id}",
                f.ThreatScore ?? 0, HazardLevelLogic.From(f.ThreatScore)))
            .ToList();

        // 8) time series
        var firstOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var cutoffDate = firstOfMonth.AddMonths(-(TimeSeriesMonths - 1));

        var measureTimestamps = await db.PersonDocs
            .Where(d => (isLeadership || !d.Person!.IsClassified) && d.Timestamp >= cutoffDate)
            .Select(d => d.Timestamp)
            .ToListAsync(cancellationToken);
        var newEntryTimestamps = await db.People
            .Where(p => (isLeadership || !p.IsClassified) && p.CreatedAt >= cutoffDate)
            .Select(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        var deDe = CultureInfo.GetCultureInfo("de-DE");
        var timeSeries = new List<StatisticsMonth>(TimeSeriesMonths);
        for (var i = 0; i < TimeSeriesMonths; i++)
        {
            var source = cutoffDate.AddMonths(i);
            var until = source.AddMonths(1);
            var measures = measureTimestamps.Count(z => z >= source && z < until);
            var newEntries = newEntryTimestamps.Count(z => z >= source && z < until);
            timeSeries.Add(new StatisticsMonth(source.Year, source.Month, source.ToString("MMM yy", deDe), measures, newEntries));
        }

        return new StatisticsReport(metrics, peopleByClassification, peopleByHazard, peopleByLifeStatus,
            factionsByHazard, measureOutcomes, casesByStatus, topPeople, topFactions, timeSeries);
    }

    // in-memory bucket
    private static async Task<List<DistributionSegment>> HazardDistributionAsync(IQueryable<int?> scores,
        CancellationToken cancellationToken)
    {
        var values = await scores.ToListAsync(cancellationToken);
        var count = values
            .GroupBy(HazardLevelLogic.From)
            .ToDictionary(g => g.Key, g => g.Count());
        return HazardLevelLogic.All
            .Select(s => new DistributionSegment(HazardLevelLogic.Name(s), count.GetValueOrDefault(s)))
            .ToList();
    }
}
