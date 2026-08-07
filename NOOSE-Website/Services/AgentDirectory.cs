using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;

namespace NOOSE_Website.Services;

/// <summary>Roster-backed agent options for the "Agent" filter dropdowns.</summary>
/// <remarks>
/// Log tables carry a denormalized actor name that is blank for never-released accounts and stale after a
/// rename, so filters resolve the name here. This is the only consumer of AgentSelection's Listable rule:
/// terminated and blocked agents stay listed, because their past actions are exactly what these filters
/// exist for. Team leads and partners are excluded like everywhere else.
/// </remarks>
public static class AgentDirectory
{
    /// <summary>Every agent selectable in a filter, ordered by codename.</summary>
    public static async Task<List<(string Id, string Codename)>> AllAsync(
        AppDbContext db, CancellationToken cancellationToken = default)
    {
        var rows = await Listable(db).Select(u => new { u.Id, u.Codename }).ToListAsync(cancellationToken);
        return rows.Select(r => (r.Id, r.Codename)).ToList();
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
        var rows = await Listable(db).Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.Codename }).ToListAsync(cancellationToken);
        return rows.Select(r => (r.Id, r.Codename)).ToList();
    }

    private static IQueryable<Agent> Listable(AppDbContext db)
        => db.Users.AsNoTracking().OnlyListable().OrderBy(u => u.Codename);
}
