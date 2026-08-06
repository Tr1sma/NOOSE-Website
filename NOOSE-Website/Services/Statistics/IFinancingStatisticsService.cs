using NOOSE_Website.Models.Statistics;

namespace NOOSE_Website.Services.Statistics;

/// <summary>Aggregated funding statistics (volume over time, budget utilisation, top positions).</summary>
public interface IFinancingStatisticsService
{
    Task<FinancingStatistics> GetAsync(StatisticsScope scope, CancellationToken cancellationToken = default);

    /// <summary>Figures of one closed calendar month, for the archived situation report.</summary>
    Task<FinancingReport> GetMonthAsync(int year, int month, CancellationToken cancellationToken = default);
}
