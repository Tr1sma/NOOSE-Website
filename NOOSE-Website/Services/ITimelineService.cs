using System.Security.Claims;
using NOOSE_Website.Models.Timeline;

namespace NOOSE_Website.Services;

/// <summary>Unified chronological timeline of a record, merging audit and domain events. Read-only.</summary>
public interface ITimelineService
{
    /// <summary>All visible events of the record, newest first; empty if the viewer may not see it.</summary>
    Task<IReadOnlyList<TimelineEntry>> GetTimelineAsync(
        string entityType, string entityId, ClaimsPrincipal viewer,
        CancellationToken cancellationToken = default);
}
