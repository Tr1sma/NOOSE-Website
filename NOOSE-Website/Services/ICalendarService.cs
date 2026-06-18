using System.Security.Claims;
using NOOSE_Website.Models.Calendar;

namespace NOOSE_Website.Services;

/// <summary>Read-only aggregation of calendar entries for a window across all dated records; each source keeps its canonical visibility, so non-visible entries are absent (no leak).</summary>
public interface ICalendarService
{
    /// <summary>Calendar entries visible to the viewer in the given mode that touch the UTC window.</summary>
    Task<IReadOnlyList<CalendarEntry>> GetEntriesAsync(
        DateTime sourceUtc, DateTime untilUtc, ClaimsPrincipal viewer, CalendarMode mode, CancellationToken cancellationToken = default);
}
