using NOOSE_Website.Models.Dashboard;

namespace NOOSE_Website.Models.Statistics;

/// <summary>Aggregated abduction statistics for the statistics page.</summary>
public record AbductionStatistics(
    int Total,
    int WithLeak,
    int ActiveCompromised,
    ChartGrid OverTime,
    ChartGrid Outcomes,
    ChartGrid TopPerpetrators,
    IReadOnlyList<DistributionSegment> Severity)
{
    public static AbductionStatistics Empty { get; } =
        new(0, 0, 0, ChartGrid.Empty, ChartGrid.Empty, ChartGrid.Empty, []);
}
