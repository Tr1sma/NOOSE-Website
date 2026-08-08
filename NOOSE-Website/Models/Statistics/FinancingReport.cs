using NOOSE_Website.Models.Dashboard;

namespace NOOSE_Website.Models.Statistics;

/// <summary>Funding figures of one month, archived alongside a situation report.</summary>
/// <remarks>
/// Persisted as JSON in SituationReport.FinancingJson. Reports written before this feature carry no
/// value at all, and older snapshots may miss members added later, so every member is read as nullable.
/// </remarks>
public record FinancingReport(
    decimal ApprovedSubsidy,
    decimal PaidSubsidy,
    int RequestCount,
    int ApprovedCount,
    int RejectedCount,
    IReadOnlyList<DistributionSegment>? TopItems,
    IReadOnlyList<FinancingRankUtilisation>? ByRank)
{
    public static FinancingReport Empty { get; } = new(0m, 0m, 0, 0, 0, [], []);
}

/// <summary>Budget versus consumption of one rank in the reported month.</summary>
public record FinancingRankUtilisation(string Rank, decimal Budget, decimal Consumed);
