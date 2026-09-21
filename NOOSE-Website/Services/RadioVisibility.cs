using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Radio;

namespace NOOSE_Website.Services;

/// <summary>Read and write gate of the radio plan. One expression, read by the plan and by the search alike.</summary>
/// <remarks>
/// Two conditions: a classified channel is leadership-only, and a channel bound to a taskforce is only as visible as
/// that taskforce — the same need-to-know rule <see cref="TaskforceVisibility"/> applies to the taskforce itself, in
/// the same subquery shape. It lives in one place because the plan page and the reverse search both ask it; two
/// spellings of one gate is how the stricter of them quietly stops being enforced.
/// <para>
/// Partners never reach any of this: the service requires an internal agent, the provider excludes them, and the
/// type carries no <c>PartnerShare</c> path at all.
/// </para>
/// </remarks>
public static class RadioVisibility
{
    /// <summary>Channels the viewer may see.</summary>
    public static IQueryable<RadioChannel> OnlyVisible(this IQueryable<RadioChannel> query, AppDbContext db, ViewerScope scope)
    {
        // locals so EF parameterizes rather than baking the viewer's flags into the SQL
        bool mayClassified = scope.MayClassifiedRead, mayAllTaskforces = scope.MayAllTaskforces;
        var meId = scope.MeId;
        return query.Where(c => (!c.IsClassified || mayClassified)
            && (c.TaskforceId == null
                || mayAllTaskforces
                || (meId != null && db.TaskforceAgents.Any(ta => ta.TaskforceId == c.TaskforceId && ta.AgentId == meId))));
    }

    /// <summary>May create or change a row of this secrecy. The UI gate and the service guard read this one predicate.</summary>
    /// <remarks>
    /// Setting the flag is the same question as editing a set one: a classified channel a junior created would
    /// vanish from that junior's own plan the moment it was saved.
    /// </remarks>
    public static bool MayEdit(ClaimsPrincipal actor, bool touchesClassified)
        => actor.MayWrite() && (!touchesClassified || actor.IsLeadership());
}
