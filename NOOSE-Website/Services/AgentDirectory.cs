using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Roster-backed agent options for the "Agent" filter dropdowns.</summary>
/// <remarks>
/// Log tables carry a denormalized actor name that is blank for never-released accounts and stale after a
/// rename, so filters resolve the name here. Terminated and blocked agents stay listed: their past actions
/// are exactly what these filters exist for. Two flavours: <see cref="AllAsync"/> hides read-only supervision
/// RP-wide (agent-facing filters), <see cref="AllForAuditAsync"/> shows supervision and partners because the
/// audit viewer is leadership-only.
/// </remarks>
public static class AgentDirectory
{
    /// <summary>Every agent selectable in a filter, ordered by codename.</summary>
    public static async Task<List<(string Id, string Codename)>> AllAsync(
        AppDbContext db, CancellationToken cancellationToken = default)
    {
        var rows = await Selectable(db).Select(u => new { u.Id, u.Codename }).ToListAsync(cancellationToken);
        return rows.Select(r => (r.Id, r.Codename)).ToList();
    }

    /// <summary>Audit-viewer flavour: additionally lists read-only supervision and partner accounts, ordered by codename.</summary>
    public static async Task<List<(string Id, string Codename, bool IsSupervision, PartnerAgency? Agency)>> AllForAuditAsync(
        AppDbContext db, CancellationToken cancellationToken = default)
    {
        var rows = await db.Users.AsNoTracking()
            .Where(u => (!string.IsNullOrEmpty(u.Codename) && (!u.IsTeamLead || u.IsAdmin))
                        || u.IsTeamLead || u.PartnerAgency != null)
            .OrderBy(u => u.Codename)
            .Select(u => new { u.Id, u.Codename, IsSupervision = u.IsTeamLead && !u.IsAdmin, u.PartnerAgency })
            .ToListAsync(cancellationToken);
        return rows.Select(r => (r.Id, r.Codename, r.IsSupervision, r.PartnerAgency)).ToList();
    }

    /// <summary>Selectable agents narrowed to the given actor ids, ordered by codename.</summary>
    public static async Task<List<(string Id, string Codename)>> ByIdsAsync(
        AppDbContext db, IEnumerable<string> actorIds, CancellationToken cancellationToken = default)
    {
        var ids = actorIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }
        var rows = await Selectable(db).Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.Codename }).ToListAsync(cancellationToken);
        return rows.Select(r => (r.Id, r.Codename)).ToList();
    }

    // blank codename = never released (applicant/pending), no agent; IsTeamLead && !IsAdmin = read-only supervisor, hidden RP-wide
    private static IQueryable<Agent> Selectable(AppDbContext db)
        => db.Users.AsNoTracking()
            .Where(u => !string.IsNullOrEmpty(u.Codename) && (!u.IsTeamLead || u.IsAdmin))
            .OrderBy(u => u.Codename);
}
