using System.Security.Claims;
using NOOSE_Website.Models.Threat;

namespace NOOSE_Website.Services;

/// <summary>Read-only threat-score time series derived from <c>ThreatScoreHistory</c>. Read-only.</summary>
public interface IThreatTrendService
{
    /// <summary>Score/confidence points of one record over the last <paramref name="days"/>, oldest first.</summary>
    Task<IReadOnlyList<ThreatScorePoint>> GetHistoryAsync(
        string entityType, string entityId, int days = 180, CancellationToken cancellationToken = default);

    /// <summary>Last <paramref name="points"/> scores per id, one bundled query (list sparklines).</summary>
    Task<IReadOnlyDictionary<string, IReadOnlyList<int>>> GetSparklinesAsync(
        string entityType, IReadOnlyCollection<string> ids, int points = 8, CancellationToken cancellationToken = default);

    /// <summary>Top-N factions ranked per month for a bar-chart race; VS-filtered for the viewer.</summary>
    Task<IReadOnlyList<ThreatRaceFrame>> GetFactionRaceAsync(
        ClaimsPrincipal actor, int months = 12, int topN = 12, CancellationToken cancellationToken = default);

    /// <summary>Records whose score rose the most within the window; VS-filtered for the viewer.</summary>
    Task<IReadOnlyList<ThreatMover>> GetTopMoversAsync(
        ClaimsPrincipal actor, int windowDays = 30, int topN = 10, CancellationToken cancellationToken = default);
}
