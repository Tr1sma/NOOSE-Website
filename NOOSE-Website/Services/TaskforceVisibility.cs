using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Taskforces;

namespace NOOSE_Website.Services;

/// <summary>Central taskforce visibility rule: an agent sees only assigned taskforces; leadership/admin sees all.</summary>
public static class TaskforceVisibility
{
    /// <summary>Filters a taskforce query to the entries visible to the caller.</summary>
    public static IQueryable<Taskforce> OnlyVisible(this IQueryable<Taskforce> query, AppDbContext db, bool mayAll, string? meId)
    {
        if (mayAll)
        {
            return query;
        }
        if (string.IsNullOrEmpty(meId))
        {
            return query.Where(_ => false);
        }
        return query.Where(t => db.TaskforceAgents.Any(ta => ta.TaskforceId == t.Id && ta.AgentId == meId));
    }

    /// <summary>True if the taskforce exists and is visible to the caller (leadership/admin or member).</summary>
    public static async Task<bool> IsVisibleAsync(AppDbContext db, string taskforceId, bool mayAll, string? meId, CancellationToken cancellationToken = default)
    {
        if (!await db.Taskforces.AnyAsync(t => t.Id == taskforceId, cancellationToken))
        {
            return false;
        }
        if (mayAll)
        {
            return true;
        }
        return !string.IsNullOrEmpty(meId)
            && await db.TaskforceAgents.AnyAsync(ta => ta.TaskforceId == taskforceId && ta.AgentId == meId, cancellationToken);
    }

    /// <summary>Visible taskforce ids from a candidate set (for batch reference resolution).</summary>
    public static async Task<HashSet<string>> VisibleIdsAsync(AppDbContext db, IReadOnlyCollection<string> taskforceIds, bool mayAll, string? meId, CancellationToken cancellationToken = default)
    {
        if (taskforceIds.Count == 0)
        {
            return new();
        }
        if (mayAll)
        {
            return taskforceIds.ToHashSet();
        }
        if (string.IsNullOrEmpty(meId))
        {
            return new();
        }
        var visible = await db.TaskforceAgents
            .Where(ta => ta.AgentId == meId && taskforceIds.Contains(ta.TaskforceId))
            .Select(ta => ta.TaskforceId)
            .Distinct()
            .ToListAsync(cancellationToken);
        return visible.ToHashSet();
    }
}
