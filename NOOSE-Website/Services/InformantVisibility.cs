using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;

namespace NOOSE_Website.Services;

/// <summary>Informant visibility. Fail-closed: strangers and partners see nothing; leadership, read-only supervision and
/// the assigned handler see the whole record. There is no second tier — record access implies full detail.</summary>
public static class InformantVisibility
{
    /// <summary>May see the informant record at all — and with it every field on it.</summary>
    public static bool MaySeeRecord(ClaimsPrincipal actor, string? handlerId)
    {
        if (actor.IsPartner())
        {
            return false;
        }
        if (actor.IsLeadership() || actor.IsOnlyReader())
        {
            return true;
        }
        var me = actor.GetAgentId();
        return handlerId is not null && me is not null && me == handlerId;
    }

    /// <summary>May the viewer scope see the informant record, from a scope rather than a principal.</summary>
    /// <remarks>Deliberately narrower than the principal overload: a scope cannot express read-only supervision, so
    /// this arm leaves it out. Never a leak, only an omission — and none for the assistant, which
    /// <see cref="Permission.RequireLlmUse" /> bars supervision from entirely.</remarks>
    public static bool MaySeeRecord(ViewerScope scope, string? handlerId)
        => !scope.IsPartner
        && (scope.IsLeadership || (handlerId is { Length: > 0 } && handlerId == scope.MeId));

    /// <summary>May create/edit meetings + record fields.</summary>
    public static bool MayWrite(ClaimsPrincipal actor, string? handlerId)
    {
        if (actor.IsOnlyReader() || actor.IsPartner())
        {
            return false;
        }
        var me = actor.GetAgentId();
        return actor.IsLeadership() || (handlerId is not null && me is not null && me == handlerId);
    }

    /// <summary>Ids of informants the actor may see (fail-closed).</summary>
    public static async Task<List<string>> VisibleIdsAsync(AppDbContext db, ClaimsPrincipal actor, CancellationToken ct)
    {
        if (actor.IsPartner())
        {
            return new List<string>();
        }
        var q = db.Informants.AsQueryable();
        if (!(actor.IsLeadership() || actor.IsOnlyReader()))
        {
            var me = actor.GetAgentId();
            if (me is null)
            {
                return new List<string>();
            }
            q = q.Where(i => i.HandlerId == me);
        }
        return await q.Select(i => i.Id).ToListAsync(ct);
    }
}
