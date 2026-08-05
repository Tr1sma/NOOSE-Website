using System.Security.Claims;
using NOOSE_Website.Models.Statistics;

namespace NOOSE_Website.Services.Statistics;

/// <summary>Personnel analytics for the leadership section of the statistics page.</summary>
/// <remarks>
/// Every method is gated with <see cref="Permission.RequireClassifiedRead"/>, matching the
/// LeadershipPage policy that wraps the section, and the attendance service that came before it.
/// Agents are identified by codename only — the real name never leaves this layer.
/// </remarks>
public interface IWorkforceStatisticsService
{
    /// <summary>Head count per rank, lowest rank last, as a pyramid.</summary>
    Task<ChartGrid> GetRankPyramidAsync(ClaimsPrincipal actor, StatisticsScope scope,
        CancellationToken cancellationToken = default);

    /// <summary>Promotions per bucket, from the rank history.</summary>
    Task<ChartGrid> GetPromotionTrendAsync(ClaimsPrincipal actor, StatisticsScope scope,
        CancellationToken cancellationToken = default);

    /// <summary>Recorded edits per agent per bucket; the workload heatmap.</summary>
    Task<ChartMatrix> GetWorkloadAsync(ClaimsPrincipal actor, StatisticsScope scope,
        CancellationToken cancellationToken = default);

    /// <summary>Unexcused misses per agent per bucket, counted over closed meetings.</summary>
    /// <remarks>
    /// Counts rather than states, so the heatmap encodes a magnitude. A grid of attendance *states* would
    /// need a status scale, which a sequential ramp would misrepresent.
    /// </remarks>
    Task<ChartMatrix> GetMissedMeetingsAsync(ClaimsPrincipal actor, StatisticsScope scope,
        CancellationToken cancellationToken = default);

    /// <summary>Absence days per calendar day across the window.</summary>
    Task<IReadOnlyList<ChartDay>> GetAbsenceCalendarAsync(ClaimsPrincipal actor, StatisticsScope scope,
        CancellationToken cancellationToken = default);

    /// <summary>Applications per stage, in funnel order.</summary>
    Task<ChartGrid> GetRecruitingFunnelAsync(ClaimsPrincipal actor, StatisticsScope scope,
        CancellationToken cancellationToken = default);
}
