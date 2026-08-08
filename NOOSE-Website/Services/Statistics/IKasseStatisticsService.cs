using NOOSE_Website.Models.Statistics;

namespace NOOSE_Website.Services.Statistics;

/// <summary>Aggregated treasury statistics (balances, movements over time, top reasons).</summary>
public interface IKasseStatisticsService
{
    Task<KasseStatistics> GetAsync(StatisticsScope scope, CancellationToken cancellationToken = default);
}
