using System.Security.Claims;
using NOOSE_Website.Authorization;

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
}
