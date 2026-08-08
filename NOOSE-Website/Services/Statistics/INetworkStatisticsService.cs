using NOOSE_Website.Models.Statistics;

namespace NOOSE_Website.Services.Statistics;

/// <summary>Analytics over the links between records: what is connected to what, and how.</summary>
public interface INetworkStatisticsService
{
    /// <summary>Link counts as a record-type by record-type cross-tab.</summary>
    Task<ChartMatrix> GetTypeCrossTabAsync(StatisticsScope scope, CancellationToken cancellationToken = default);

    /// <summary>New links per bucket, split by kind (default, conflict, alliance).</summary>
    Task<ChartGrid> GetLinkKindTrendAsync(StatisticsScope scope, CancellationToken cancellationToken = default);

    /// <summary>Person-to-person relations by type.</summary>
    Task<ChartGrid> GetRelationTypesAsync(StatisticsScope scope, CancellationToken cancellationToken = default);

    /// <summary>Factions sized by member count and coloured by hazard, for the treemap.</summary>
    Task<IReadOnlyList<ChartTile>> GetFactionTilesAsync(StatisticsScope scope,
        CancellationToken cancellationToken = default);

    /// <summary>Classification changes as directed flows, for the Sankey.</summary>
    Task<IReadOnlyList<ChartFlow>> GetClassificationFlowsAsync(StatisticsScope scope,
        CancellationToken cancellationToken = default);
}
