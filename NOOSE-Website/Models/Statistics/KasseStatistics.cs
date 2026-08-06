using NOOSE_Website.Models.Dashboard;

namespace NOOSE_Website.Models.Statistics;

/// <summary>Aggregated treasury statistics for the statistics page.</summary>
public record KasseStatistics(
    decimal SchwarzgeldBalance,
    decimal GruengeldBalance,
    decimal TotalDeposits,
    decimal TotalWithdrawals,
    ChartGrid MovementsOverTime,
    ChartGrid Balances,
    IReadOnlyList<DistributionSegment> TopReasons)
{
    public static KasseStatistics Empty { get; } =
        new(0m, 0m, 0m, 0m, ChartGrid.Empty, ChartGrid.Empty, []);
}
