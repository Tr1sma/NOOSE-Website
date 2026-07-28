using NOOSE_Website.Data.Entities.Absences;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Central absence visibility: own always, the roster for peers, everything for leadership.</summary>
public static class AbsenceVisibility
{
    public static IQueryable<Absence> OnlyVisible(this IQueryable<Absence> query, AbsenceViewScope scope, string? meId)
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
        return query.Where(a => a.AgentId == meId
            || (a.Agent!.Status == AgentStatus.Active && !a.Agent.IsTeamLead && a.Agent.PartnerAgency == null));
    }

    /// <summary>Foreign absences peers may see; TeamLeads stay RP-invisible.</summary>
    public static IQueryable<Absence> RosterVisible(this IQueryable<Absence> query)
        => query.Where(a => a.Agent!.Status == AgentStatus.Active
                         && !a.Agent.IsTeamLead && a.Agent.PartnerAgency == null);

    /// <summary>Absences covering a calendar day; both bounds are inclusive.</summary>
    public static IQueryable<Absence> Covering(this IQueryable<Absence> query, DateOnly day)
        => query.Where(a => a.FromDate <= day && a.ToDate >= day);
}
