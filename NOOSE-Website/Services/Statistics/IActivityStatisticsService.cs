using System.Security.Claims;
using NOOSE_Website.Models.Statistics;

namespace NOOSE_Website.Services.Statistics;

/// <summary>Activity analytics over the audit log: when work happens and which record types it touches.</summary>
/// <remarks>
/// Every method is leadership-gated, because that is the rule the audit log carries everywhere else in the
/// codebase (AuditLogQueryService, the counter-intelligence cockpit). Aggregates are anonymous — no agent
/// and no record is named here; per-agent workload lives in the personnel service instead.
/// </remarks>
public interface IActivityStatisticsService
{
    /// <summary>Weekday by hour-of-day counts in local time; the classic duty-rhythm grid.</summary>
    Task<ChartMatrix> GetWeekdayHourAsync(ClaimsPrincipal actor, StatisticsScope scope,
        CancellationToken cancellationToken = default);

    /// <summary>Per-day counts across the window, for the calendar heatmap.</summary>
    Task<IReadOnlyList<ChartDay>> GetDailyDensityAsync(ClaimsPrincipal actor, StatisticsScope scope,
        CancellationToken cancellationToken = default);

    /// <summary>Edits per bucket, one series per record type; shows where the effort goes.</summary>
    Task<ChartGrid> GetByRecordTypeAsync(ClaimsPrincipal actor, StatisticsScope scope,
        CancellationToken cancellationToken = default);

    /// <summary>Record type by bucket, so neglected record kinds become visible as cold rows.</summary>
    Task<ChartMatrix> GetCaptureGapsAsync(ClaimsPrincipal actor, StatisticsScope scope,
        CancellationToken cancellationToken = default);
}
