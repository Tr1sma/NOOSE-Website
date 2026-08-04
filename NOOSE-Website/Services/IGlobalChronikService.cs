using System.Security.Claims;
using NOOSE_Website.Models.Timeline;

namespace NOOSE_Website.Services;

/// <summary>Agency-wide chronicle of record-level lifecycle + classification events. Read-only, VS-filtered.</summary>
public interface IGlobalChronikService
{
    /// <summary>Visible record events within the window, newest first; empty for partners.</summary>
    Task<ChronikResult> GetEventsAsync(ChronikQuery query, ClaimsPrincipal viewer, CancellationToken cancellationToken = default);

    /// <summary>Selectable record types + acting agents for the chronicle filter.</summary>
    Task<ChronikFilterOptions> GetFilterOptionsAsync(ClaimsPrincipal viewer, CancellationToken cancellationToken = default);
}
