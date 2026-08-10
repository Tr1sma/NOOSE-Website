using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Taskforces;

namespace NOOSE_Website.Services;

/// <summary>The extra gate a source carries on top of its parent record: the "nur intern" flag.</summary>
/// <remarks>
/// The parent gate is <see cref="Visibility.IsRecordVisibleAsync"/>; this only decides the flag. The dialog offers
/// it exclusively on a taskforce (<c>SourceDialog.ShowInternalToggle</c>), and its label promises two things —
/// taskforce-internal, and not for partners. Both are enforced here, and the partner half now holds at every parent
/// type rather than only on a taskforce, which is what the label already claimed.
/// </remarks>
public static class SourceVisibility
{
    /// <summary>Taskforce ids the viewer belongs to; empty for partners and for a viewer without agent context.</summary>
    public static async Task<HashSet<string>> MyTaskforceIdsAsync(
        AppDbContext db, ViewerScope scope, CancellationToken cancellationToken = default)
    {
        if (scope.MeId is not { Length: > 0 } meId || scope.IsPartner)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }
        var ids = await db.TaskforceAgents
            .Where(ta => ta.AgentId == meId)
            .Select(ta => ta.TaskforceId)
            .Distinct()
            .ToListAsync(cancellationToken);
        return ids.ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Whether the viewer may see this source, given they may already see its parent.</summary>
    public static bool MaySee(Source source, string parentType, string parentId, ViewerScope scope,
        IReadOnlySet<string> myTaskforceIds)
    {
        if (!source.IsInternalOnly)
        {
            return true;
        }
        // "not for partners" holds wherever the source hangs, not just on a taskforce
        if (scope.IsPartner)
        {
            return false;
        }
        // no owning taskforce to be a member of: the flag has no second half to enforce here
        return parentType != nameof(Taskforce) || myTaskforceIds.Contains(parentId);
    }

    /// <summary>Drops the sources of one parent that the viewer may not see.</summary>
    public static List<Source> OnlyVisible(this List<Source> sources, string parentType, string parentId,
        ViewerScope scope, IReadOnlySet<string> myTaskforceIds)
        => sources.Where(s => MaySee(s, parentType, parentId, scope, myTaskforceIds)).ToList();
}
