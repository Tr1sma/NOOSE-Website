using NOOSE_Website.Models.Statistics;

namespace NOOSE_Website.Services.Statistics;

/// <summary>Aggregated abduction statistics (counts, outcomes, leak severity, top perpetrators).</summary>
public interface IAbductionStatisticsService
{
    Task<AbductionStatistics> GetAsync(StatisticsScope scope, CancellationToken cancellationToken = default);
}
