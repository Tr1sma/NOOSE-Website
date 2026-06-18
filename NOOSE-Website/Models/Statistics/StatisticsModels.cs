using NOOSE_Website.Models.Dashboard;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Statistics;

/// <summary>A top-list entry (most dangerous people/factions); only scored records (score &gt; 0) appear.</summary>
public record StatisticsTopEntry(string Name, string CaseNumber, string Href, int Score, HazardLevel Level);

/// <summary>One month of the 12-month series: measures (person docs) and new person records.</summary>
public record StatisticsMonth(int Year, int Month, string Label, int Measures, int NewEntries);

/// <summary>Aggregated statistics; classification-filtered per caller, deterministic segment order for stable colours.</summary>
public record StatisticsReport(
    DashboardMetrics Metrics,
    IReadOnlyList<DistributionSegment> PeopleByClassification,
    IReadOnlyList<DistributionSegment> PeopleByHazard,
    IReadOnlyList<DistributionSegment> PeopleByLifeStatus,
    IReadOnlyList<DistributionSegment> FactionsByHazard,
    IReadOnlyList<DistributionSegment> MeasureOutcomes,
    IReadOnlyList<DistributionSegment> CasesByStatus,
    IReadOnlyList<StatisticsTopEntry> TopPeople,
    IReadOnlyList<StatisticsTopEntry> TopFactions,
    IReadOnlyList<StatisticsMonth> TimeSeries);

/// <summary>Header of an archived situation report (without the heavy JSON snapshot).</summary>
public record SituationReportHead(string Id, int Year, int Month, string Title, DateTime GeneratedAt, string? GeneratedBy);

/// <summary>An archived situation report for the detail view: header plus the frozen report snapshot.</summary>
public record SituationReportDisplay(string Id, string Title, DateTime GeneratedAt, string? GeneratedBy, StatisticsReport Report);
