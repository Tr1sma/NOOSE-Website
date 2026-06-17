using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Jobs;

namespace NOOSE_Website.Services;

/// <summary>Central job visibility rule. Always filter through here, never copy the predicate.</summary>
public static class JobVisibility
{
    /// <summary>Filters a job query to entries visible to the caller (restricted only for involved/supervision).</summary>
    public static IQueryable<Job> OnlyVisible(this IQueryable<Job> query, AppDbContext db, bool mayAll, string? meId)
    {
        if (mayAll)
        {
            return query;
        }
        if (string.IsNullOrEmpty(meId))
        {
            // fail-closed: only unrestricted jobs
            return query.Where(a => !a.IsRestricted);
        }
        return query.Where(a => !a.IsRestricted
            || a.CreatedById == meId
            || db.JobAssignments.Any(z => z.JobId == a.Id && z.AgentId == meId));
    }

    /// <summary>From a candidate set, the job ids visible to the caller (for batch reference resolution).</summary>
    public static async Task<HashSet<string>> VisibleIdsAsync(AppDbContext db, IReadOnlyCollection<string> jobIds,
        bool mayAll, string? meId, CancellationToken cancellationToken = default)
    {
        if (jobIds.Count == 0)
        {
            return new();
        }
        if (mayAll)
        {
            return jobIds.ToHashSet();
        }
        var hasMe = !string.IsNullOrEmpty(meId);
        var visible = await db.Jobs
            .Where(a => jobIds.Contains(a.Id)
                && (!a.IsRestricted
                    || (hasMe && (a.CreatedById == meId
                        || db.JobAssignments.Any(z => z.JobId == a.Id && z.AgentId == meId)))))
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);
        return visible.ToHashSet();
    }
}
