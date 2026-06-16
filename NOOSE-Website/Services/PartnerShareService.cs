using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IPartnerShareService" />
public class PartnerShareService(IDbContextFactory<AppDbContext> dbFactory) : IPartnerShareService
{
    public async Task<IReadOnlyList<PartnerShareState>> GetForRecordAsync(string entityType, string entityId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var shares = await db.PartnerShares.AsNoTracking()
            .Where(s => s.EntityType == entityType && s.EntityId == entityId)
            .ToListAsync(cancellationToken);
        return BuildStates(shares);
    }

    public async Task<IReadOnlyList<PartnerShareState>> GetForChildAsync(string childType, string childId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var shares = await db.PartnerShares.AsNoTracking()
            .Where(s => s.EntityType == childType && s.EntityId == childId)
            .ToListAsync(cancellationToken);
        return BuildStates(shares);
    }

    public async Task SetParentAsync(string entityType, string entityId, PartnerAgency agency, bool released, bool includesChildren,
        ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await UpsertAsync(db, entityType, entityId, agency, released, includesChildren, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetChildAsync(string childType, string childId, PartnerAgency agency, bool released,
        ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await UpsertAsync(db, childType, childId, agency, released, includesChildren: false, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task UpsertAsync(AppDbContext db, string entityType, string entityId, PartnerAgency agency,
        bool released, bool includesChildren, CancellationToken cancellationToken)
    {
        var row = await db.PartnerShares
            .FirstOrDefaultAsync(s => s.EntityType == entityType && s.EntityId == entityId && s.Agency == agency, cancellationToken);
        if (released)
        {
            if (row is null)
            {
                db.PartnerShares.Add(new PartnerShare
                {
                    EntityType = entityType,
                    EntityId = entityId,
                    Agency = agency,
                    IncludesChildren = includesChildren,
                });
            }
            else
            {
                row.IncludesChildren = includesChildren;
            }
        }
        else if (row is not null)
        {
            // soft-delete via interceptor
            db.PartnerShares.Remove(row);
        }
    }

    private static IReadOnlyList<PartnerShareState> BuildStates(List<PartnerShare> shares)
        => PartnerAgencyDisplay.All
            .Select(agency =>
            {
                var row = shares.FirstOrDefault(s => s.Agency == agency);
                return new PartnerShareState(agency, row is not null, row?.IncludesChildren ?? false);
            })
            .ToList();
}
