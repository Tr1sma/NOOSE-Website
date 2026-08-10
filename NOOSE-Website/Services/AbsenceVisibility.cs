using System.Security.Claims;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Absences;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Central absence visibility: own always, the roster for peers, everything for leadership.</summary>
/// <remarks>
/// The roster clause is AgentSelection's, reached through a correlated EXISTS: a navigation property
/// cannot consume the shared IQueryable predicate, and the own-row case needs it as one side of an OR.
/// </remarks>
public static class AbsenceVisibility
{
    public static IQueryable<Absence> OnlyVisible(this IQueryable<Absence> query, AppDbContext db,
        AbsenceViewScope scope, string? meId)
    {
        if (scope == AbsenceViewScope.All)
        {
            return query;
        }
        // Fail-closed without agent context.
        if (string.IsNullOrEmpty(meId))
        {
            return query.Where(_ => false);
        }
        if (scope == AbsenceViewScope.Own)
        {
            return query.Where(a => a.AgentId == meId);
        }
        // own row always; peers only from the in-house roster
        var roster = db.Users.OnlySelectable();
        return query.Where(a => a.AgentId == meId || roster.Any(u => u.Id == a.AgentId));
    }

    /// <summary>Foreign absences peers may see; TeamLeads stay RP-invisible.</summary>
    public static IQueryable<Absence> RosterVisible(this IQueryable<Absence> query, AppDbContext db)
    {
        var roster = db.Users.OnlySelectable();
        return query.Where(a => roster.Any(u => u.Id == a.AgentId));
    }

    /// <summary>Absences covering a calendar day; both bounds are inclusive.</summary>
    public static IQueryable<Absence> Covering(this IQueryable<Absence> query, DateOnly day)
        => query.Where(a => a.FromDate <= day && a.ToDate >= day);

    /// <summary>The tier a viewer actually gets: only leadership and oversight ever reach <see cref="AbsenceViewScope.All"/>.</summary>
    /// <remarks>The page asks, the principal decides. Lives here rather than in the service because the search needs
    /// the same answer, and a second copy of this ternary is a second chance to widen it by accident.</remarks>
    public static AbsenceViewScope Granted(ClaimsPrincipal viewer, AbsenceViewScope requested)
        => requested == AbsenceViewScope.All && !viewer.MayClassifiedRead()
            ? AbsenceViewScope.Team
            : requested;

    /// <summary>Whether the viewer may read an absence's owner-only fields: the free-text reason and the
    /// acknowledgement signal.</summary>
    /// <remarks>Named because it is easy to miss: <see cref="AbsenceViewScope.Team"/> grants the row but not these
    /// fields — the roster tier is "who is away", not "why". A reader that matches on the reason without asking here
    /// hands peers free text they were never shown on the page.</remarks>
    public static bool MayReadPrivateFields(AbsenceViewScope granted, bool isOwnRow)
        => granted == AbsenceViewScope.All || isOwnRow;
}
