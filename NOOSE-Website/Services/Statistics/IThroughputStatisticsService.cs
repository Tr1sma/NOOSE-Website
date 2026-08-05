using NOOSE_Website.Models.Statistics;

namespace NOOSE_Website.Services.Statistics;

/// <summary>Throughput analytics: how fast work moves and whether the backlog grows.</summary>
/// <remarks>Sourced from the record tables, so everything is classification-filtered by the scope.</remarks>
public interface IThroughputStatisticsService
{
    /// <summary>New person records against recorded measures per bucket.</summary>
    Task<ChartGrid> GetCaptureVersusMeasuresAsync(StatisticsScope scope, CancellationToken cancellationToken = default);

    /// <summary>Days from opening to completion, over fixed buckets.</summary>
    Task<IReadOnlyList<ChartBucket>> GetCaseCycleTimeAsync(StatisticsScope scope,
        CancellationToken cancellationToken = default);

    /// <summary>Opened against completed cases per bucket; diverging bars means the backlog moves.</summary>
    Task<ChartGrid> GetOpenedVersusClosedAsync(StatisticsScope scope, CancellationToken cancellationToken = default);

    /// <summary>Share of follow-ups completed on or before their due date.</summary>
    Task<IReadOnlyList<ChartRatio>> GetFollowupPunctualityAsync(StatisticsScope scope,
        CancellationToken cancellationToken = default);
}
