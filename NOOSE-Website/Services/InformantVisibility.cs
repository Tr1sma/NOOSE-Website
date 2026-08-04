using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;

namespace NOOSE_Website.Services;

/// <summary>Two-tier informant visibility. Fail-closed: strangers see nothing, read-only supervision sees the codename
/// but never the identity, only leadership (with real-name rights) or the assigned handler see the real identity.</summary>
public static class InformantVisibility
{
    /// <summary>May see that the informant exists (codename tier).</summary>
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

    /// <summary>May see the real identity (identity tier). Read-only supervision never qualifies.</summary>
    public static bool MaySeeIdentity(ClaimsPrincipal actor, string? handlerId)
    {
        if (actor.IsPartner())
        {
            return false;
        }
        var me = actor.GetAgentId();
        if (handlerId is not null && me is not null && me == handlerId)
        {
            return true;
        }
        return actor.MayRealNameSee(); // leadership && !OnlyReader
    }

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
