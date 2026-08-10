using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;

namespace NOOSE_Website.Services;

/// <summary>Agenda and minutes read gate: rank/supervision see them immediately, every other internal agent once the meeting is 2h past. Partners never. Sole source of this rule.</summary>
public static class MeetingVisibility
{
    /// <summary>Hours after the meeting before its agenda opens to any internal agent.</summary>
    public const int GraceHours = 2;

    /// <summary>Instant from which the agenda is open to any internal agent: end (or start when no end) plus the grace window.</summary>
    public static DateTime PublicFrom(DateTime start, DateTime? end)
        => (end ?? start).AddHours(GraceHours);

    /// <summary>May the principal read a meeting's agenda and minutes.</summary>
    public static bool MayReadAgenda(this ClaimsPrincipal user, DateTime start, DateTime? end, DateTime nowUtc)
        => user.MayMeetingRead()                                        // rank/supervision early access
        || (!user.IsPartner() && nowUtc >= PublicFrom(start, end));     // internal-only, 2h after the meeting

    /// <summary>May the viewer scope read a meeting's agenda and minutes.</summary>
    public static bool MayReadAgenda(ViewerScope scope, DateTime start, DateTime? end, DateTime nowUtc)
        => scope.MayAgenda
        || (!scope.IsPartner && nowUtc >= PublicFrom(start, end));

    /// <summary>Of the given meetings, those whose agenda and minutes are open to the viewer right now.</summary>
    /// <remarks>Batched twin of <see cref="MayReadAgenda(ViewerScope, DateTime, DateTime?, DateTime)"/>, for callers
    /// holding many meetings at once. Absent id = closed, so a caller that forgets to look it up hides rather than
    /// shows. The times are read here rather than taken from the caller: a caller could otherwise widen the gate.</remarks>
    public static async Task<HashSet<string>> OpenIdsAsync(
        AppDbContext db, IReadOnlyCollection<string> meetingIds, ViewerScope scope, DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        if (meetingIds.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }
        // rank/supervision reads every agenda the moment it exists — no times needed
        if (scope.MayAgenda)
        {
            var all = await db.Meetings.AsNoTracking()
                .Where(m => meetingIds.Contains(m.Id))
                .Select(m => m.Id)
                .ToListAsync(cancellationToken);
            return all.ToHashSet(StringComparer.Ordinal);
        }
        if (scope.IsPartner)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }
        var rows = await db.Meetings.AsNoTracking()
            .Where(m => meetingIds.Contains(m.Id))
            .Select(m => new { m.Id, m.Start, m.End })
            .ToListAsync(cancellationToken);
        return rows
            .Where(m => nowUtc >= PublicFrom(m.Start, m.End))
            .Select(m => m.Id)
            .ToHashSet(StringComparer.Ordinal);
    }
}
