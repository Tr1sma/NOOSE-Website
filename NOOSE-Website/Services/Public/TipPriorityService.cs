using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services.Public;

/// <inheritdoc cref="ITipPriorityService" />
/// <remarks>
/// Writes through <c>ExecuteUpdateAsync</c>: a tracked write would stamp <c>GeaendertAm</c>, add an audit row and push
/// the tip onto the person's timeline on every bounty raise. Same reason as the score writes,
/// <c>FactionRecency.StampAsync</c> and the public view counter.
/// <para>
/// No <c>Permission.Require*</c> here: this is not an entry point. Every caller is a write path that has already
/// guarded itself (submission, bounty, publication, status change), and the value is derived, not entered.
/// </para>
/// <para>
/// The file names <c>FahndungKopfgeldAnteile</c> but never saves them — it reads the shares and writes
/// <c>Hinweise</c>. Anyone adding a <c>SaveChangesAsync</c> here owes the public snapshot an
/// <c>InvalidatePublicViewAsync</c>, and <c>PublicSurfaceGuardTests</c> will say so.
/// </para>
/// </remarks>
public class TipPriorityService(IDbContextFactory<AppDbContext> dbFactory) : ITipPriorityService
{
    public async Task<int> ComputeAsync(AppDbContext db, string? wantedId, int confirmedTips,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(wantedId))
        {
            return TipPriority.Compute(0m, null, confirmedTips);
        }

        var notices = await NoticesAsync(db, [wantedId], cancellationToken);
        var notice = notices.GetValueOrDefault(wantedId);
        return TipPriority.Compute(notice.Bounty, notice.Hazard, confirmedTips);
    }

    public Task StampAsync(string tipId, CancellationToken cancellationToken = default)
        => StampWhereAsync(q => q.Where(h => h.Id == tipId), cancellationToken);

    public Task StampForNoticeAsync(string wantedId, CancellationToken cancellationToken = default)
        => StampWhereAsync(q => q.Where(h => h.WantedId == wantedId), cancellationToken);

    public Task StampForCitizenAsync(string citizenProfileId, CancellationToken cancellationToken = default)
        => StampWhereAsync(q => q.Where(h => h.CitizenProfileId == citizenProfileId), cancellationToken);

    private async Task StampWhereAsync(Func<IQueryable<Hinweis>, IQueryable<Hinweis>> narrow,
        CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // decided tips need no order any more, so they are never re-stamped
        var rows = await narrow(db.Hinweise.AsNoTracking().Where(TipRules.OpenRows))
            .Select(h => new { h.Id, h.WantedId, h.CitizenProfileId, h.Priority })
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return;
        }

        var notices = await NoticesAsync(db,
            rows.Select(r => r.WantedId).OfType<string>().Distinct().ToList(), cancellationToken);
        var trust = await TrustAsync(db,
            rows.Select(r => r.CitizenProfileId).Distinct().ToList(), cancellationToken);

        foreach (var group in rows.GroupBy(r =>
                 {
                     var notice = r.WantedId is null
                         ? default
                         : notices.GetValueOrDefault(r.WantedId);
                     return TipPriority.Compute(notice.Bounty, notice.Hazard, trust.GetValueOrDefault(r.CitizenProfileId));
                 }))
        {
            var ids = group.Where(r => r.Priority != group.Key).Select(r => r.Id).ToList();
            if (ids.Count == 0)
            {
                continue; // nothing moved, no write
            }
            await db.Hinweise.Where(h => ids.Contains(h.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(h => h.Priority, group.Key), cancellationToken);
        }
    }

    private static async Task<Dictionary<string, (HazardLevel Hazard, decimal Bounty)>> NoticesAsync(
        AppDbContext db, IReadOnlyCollection<string> wantedIds, CancellationToken cancellationToken)
    {
        var map = new Dictionary<string, (HazardLevel, decimal)>(StringComparer.Ordinal);
        if (wantedIds.Count == 0)
        {
            return map;
        }

        var ids = wantedIds.ToList();
        var hazards = await db.OeffentlicheFahndungen.AsNoTracking()
            .Where(f => ids.Contains(f.Id))
            .Select(f => new { f.Id, f.PublicHazardLevel })
            .ToListAsync(cancellationToken);

        // BountyShares.Advertised is the one rule for what counts as money on a head
        var bounties = await db.FahndungKopfgeldAnteile.AsNoTracking()
            .Where(k => ids.Contains(k.WantedId))
            .Where(BountyShares.Advertised)
            .GroupBy(k => k.WantedId)
            .Select(g => new { WantedId = g.Key, Total = g.Sum(k => k.Amount) })
            .ToListAsync(cancellationToken);
        var byNotice = bounties.ToDictionary(b => b.WantedId, b => b.Total, StringComparer.Ordinal);

        foreach (var hazard in hazards)
        {
            map[hazard.Id] = (hazard.PublicHazardLevel, byNotice.GetValueOrDefault(hazard.Id));
        }
        return map;
    }

    private static async Task<Dictionary<string, int>> TrustAsync(AppDbContext db,
        IReadOnlyCollection<string> profileIds, CancellationToken cancellationToken)
    {
        if (profileIds.Count == 0)
        {
            return new(StringComparer.Ordinal);
        }

        var ids = profileIds.ToList();
        var rows = await db.BuergerProfile.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.ConfirmedTips })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(p => p.Id, p => p.ConfirmedTips, StringComparer.Ordinal);
    }
}
