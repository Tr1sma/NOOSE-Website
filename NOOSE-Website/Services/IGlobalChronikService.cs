using System.Security.Claims;
using NOOSE_Website.Models.Timeline;

namespace NOOSE_Website.Services;

/// <summary>Agency-wide chronicle of record events, fanned in from the audit log. Read-only, VS-filtered, empty for partners.</summary>
public interface IGlobalChronikService
{
    /// <summary>One page of visible events, newest first; page boundaries are whole local days.</summary>
    Task<ChronikResult> GetEventsAsync(ChronikQuery query, ClaimsPrincipal viewer, CancellationToken cancellationToken = default);

    /// <summary>Bucketed activity for the overview band: local buckets with a per-group split, gap-filled,
    /// and visibility-filtered through the same passes as the feed so bars and day headers agree.
    /// The category filter is honoured; free text is not, because it needs names the band never loads.</summary>
    Task<ChronikDensity> GetDensityAsync(ChronikQuery query, ClaimsPrincipal viewer, CancellationToken cancellationToken = default);

    /// <summary>Selectable record types + acting agents for the chronicle filter.</summary>
    Task<ChronikFilterOptions> GetFilterOptionsAsync(ClaimsPrincipal viewer, CancellationToken cancellationToken = default);
}
