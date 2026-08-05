using NOOSE_Website.Models.Dashboard;
using NOOSE_Website.Models.Statistics;

namespace NOOSE_Website.Services.Statistics;

/// <summary>Analytics over the record inventory itself: how it grows, how it is classified, how current it is.</summary>
public interface IInventoryStatisticsService
{
    /// <summary>New records per bucket, one series per record type; stacks to the inventory's growth.</summary>
    Task<ChartGrid> GetGrowthAsync(StatisticsScope scope, CancellationToken cancellationToken = default);

    /// <summary>People by classification — an ordered scale, so the colour carries the order.</summary>
    Task<ChartGrid> GetClassificationAsync(StatisticsScope scope, CancellationToken cancellationToken = default);

    /// <summary>Hazard bands for people and factions side by side.</summary>
    Task<ChartGrid> GetHazardComparisonAsync(StatisticsScope scope, CancellationToken cancellationToken = default);

    /// <summary>Effective life status; a real part-to-whole with three segments.</summary>
    Task<IReadOnlyList<DistributionSegment>> GetLifeStatusAsync(StatisticsScope scope,
        CancellationToken cancellationToken = default);

    /// <summary>Measure outcomes per bucket — the trend, not just the share.</summary>
    Task<ChartGrid> GetMeasureOutcomeTrendAsync(StatisticsScope scope, CancellationToken cancellationToken = default);

    /// <summary>Cases by status, in workflow order.</summary>
    Task<ChartGrid> GetCaseFunnelAsync(StatisticsScope scope, CancellationToken cancellationToken = default);

    /// <summary>Share of records still inside their warning window, per record type.</summary>
    Task<IReadOnlyList<ChartRatio>> GetRecencyAsync(StatisticsScope scope, CancellationToken cancellationToken = default);
}
