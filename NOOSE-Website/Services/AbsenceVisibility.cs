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
}
