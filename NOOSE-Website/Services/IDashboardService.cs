using NOOSE_Website.Models.Dashboard;

namespace NOOSE_Website.Services;

/// <summary>Dashboard data: metrics tiles and recent activity feed.</summary>
public interface IDashboardService
{
    // taskforce visibility
    /// <summary>Metrics for the four tiles.</summary>
    Task<DashboardMetrics> GetMetricsAsync(bool isLeadership, string? meId, CancellationToken cancellationToken = default);

    /// <summary>Stale records needing update, oldest first.</summary>
    Task<List<DashboardStaleRecord>> GetUpdateNeedAsync(bool isLeadership, string? meId, int max = 30, CancellationToken cancellationToken = default);

    /// <summary>Factions sorted by hazard descending.</summary>
    Task<List<DashboardFactionHazard>> GetFactionsByHazardAsync(bool isLeadership, CancellationToken cancellationToken = default);

    /// <summary>People sorted by hazard score descending.</summary>
    Task<List<DashboardFactionHazard>> GetPeopleByHazardAsync(bool isLeadership, CancellationToken cancellationToken = default);

    /// <summary>Recent changes across all records, newest first.</summary>
    Task<List<DashboardChange>> GetLastChangesAsync(bool isLeadership, string? meId, int max = 8, CancellationToken cancellationToken = default);

    /// <summary>Four distribution charts for the dashboard.</summary>
    Task<DashboardDistributions> GetDistributionsAsync(bool isLeadership, string? meId, CancellationToken cancellationToken = default);
}
