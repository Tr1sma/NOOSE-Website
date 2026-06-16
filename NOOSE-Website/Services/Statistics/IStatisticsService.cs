using NOOSE_Website.Models.Statistics;

namespace NOOSE_Website.Services.Statistics;

/// <summary>Aggregated statistics report service.</summary>
public interface IStatisticsService
{
    // taskforce visibility
    /// <summary>Builds full statistics report for caller.</summary>
    Task<StatisticsReport> GetReportAsync(bool isLeadership, string? meId, int topN = 10, CancellationToken cancellationToken = default);
}
