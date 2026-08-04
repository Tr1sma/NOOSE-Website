using System.Security.Claims;
using NOOSE_Website.Models.Timeline;

namespace NOOSE_Website.Services;

/// <summary>Agency-wide chronicle of record events, fanned in from the audit log. Read-only, VS-filtered, empty for partners.</summary>
public interface IGlobalChronikService
{
    /// <summary>One page of visible events, newest first; page boundaries are whole local days.</summary>
    Task<ChronikResult> GetEventsAsync(ChronikQuery query, ClaimsPrincipal viewer, CancellationToken cancellationToken = default);

    /// <summary>Raw event counts per day/week for the overview band. Aggregate activity only: no record,
    /// actor or classification is exposed, and the counts are deliberately not visibility-filtered
    /// so the band stays one cheap query.</summary>
    Task<IReadOnlyList<ChronikDensityBucket>> GetDensityAsync(ChronikQuery query, ClaimsPrincipal viewer, CancellationToken cancellationToken = default);

    /// <summary>Selectable record types + acting agents for the chronicle filter.</summary>
    Task<ChronikFilterOptions> GetFilterOptionsAsync(ClaimsPrincipal viewer, CancellationToken cancellationToken = default);
}
