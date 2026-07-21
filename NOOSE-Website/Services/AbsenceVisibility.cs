using NOOSE_Website.Data.Entities.Absences;

namespace NOOSE_Website.Services;

/// <summary>Central absence visibility: an agent sees only their own, leadership sees all.</summary>
public static class AbsenceVisibility
{
    public static IQueryable<Absence> OnlyVisible(this IQueryable<Absence> query, bool mayAll, string? meId)
    {
        if (mayAll)
        {
            return query;
        }
        // Fail-closed without agent context.
        if (string.IsNullOrEmpty(meId))
        {
            return query.Where(_ => false);
        }
        return query.Where(a => a.AgentId == meId);
    }

    /// <summary>Absences covering a calendar day; both bounds are inclusive.</summary>
    public static IQueryable<Absence> Covering(this IQueryable<Absence> query, DateOnly day)
        => query.Where(a => a.FromDate <= day && a.ToDate >= day);
}
