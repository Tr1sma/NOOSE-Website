using NOOSE_Website.Models.Dashboard;

namespace NOOSE_Website.Models.Statistics;

/// <summary>Aggregated funding statistics for the statistics page.</summary>
public record FinancingStatistics(
    decimal ApprovedSubsidy,
    decimal PaidSubsidy,
    int RequestCount,
    int OpenCount,
    int RejectedCount,
    ChartGrid VolumeOverTime,
    ChartGrid UtilisationByRank,
    IReadOnlyList<DistributionSegment> TopItems)
{
    /// <summary>Share of decided requests that were rejected (0-1).</summary>
    public double RejectionRate
    {
        get
        {
            var decided = RequestCount - OpenCount;
            return decided <= 0 ? 0d : (double)RejectedCount / decided;
        }
    }

    public static FinancingStatistics Empty { get; } =
        new(0m, 0m, 0, 0, 0, ChartGrid.Empty, ChartGrid.Empty, []);
}
