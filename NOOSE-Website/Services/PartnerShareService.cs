using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
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
            .Where(s => s.EntityType == entityType && s.EntityId == entityId && s.PartnerAgentId == null)
            .ToListAsync(cancellationToken);
        return BuildStates(shares);
    }

    public async Task<IReadOnlyList<PartnerShareState>> GetForChildAsync(string childType, string childId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var shares = await db.PartnerShares.AsNoTracking()
            .Where(s => s.EntityType == childType && s.EntityId == childId && s.PartnerAgentId == null)
            .ToListAsync(cancellationToken);
        return BuildStates(shares);
    }

    public async Task<IReadOnlyList<PartnerIndividualShareState>> GetIndividualSharesForRecordAsync(string entityType, string entityId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.PartnerShares.AsNoTracking()
            .Where(s => s.EntityType == entityType && s.EntityId == entityId && s.PartnerAgentId != null)
            .Join(db.Users.AsNoTracking(), s => s.PartnerAgentId, a => a.Id,
                (s, a) => new PartnerIndividualShareState(a.Id, a.Codename, s.Agency, a.PartnerRank, s.IncludesChildren))
            .OrderBy(s => s.Codename)
            .ToListAsync(cancellationToken);
        return rows;
    }

    public async Task<IReadOnlyList<PartnerAccountOption>> GetSelectablePartnersAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Users.AsNoTracking()
            .Where(a => a.PartnerAgency != null && a.Status == AgentStatus.Active)
            .OrderBy(a => a.PartnerAgency).ThenBy(a => a.Codename)
            .Select(a => new PartnerAccountOption(a.Id, a.Codename, a.PartnerAgency!.Value, a.PartnerRank))
            .ToListAsync(cancellationToken);
    }

    public async Task SetParentAsync(string entityType, string entityId, PartnerAgency agency, bool released, bool includesChildren,
        ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await UpsertAsync(db, entityType, entityId, agency, partnerAgentId: null, released, includesChildren, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetChildAsync(string childType, string childId, PartnerAgency agency, bool released,
        ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await UpsertAsync(db, childType, childId, agency, partnerAgentId: null, released, includesChildren: false, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetIndividualParentAsync(string entityType, string entityId, string partnerAgentId, bool released, bool includesChildren,
        ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // agency derived from the account
        var agency = await db.Users.Where(a => a.Id == partnerAgentId).Select(a => a.PartnerAgency).FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Das gewählte Konto ist kein Partner-Konto.");
        await UpsertAsync(db, entityType, entityId, agency, partnerAgentId, released, includesChildren, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task UpsertAsync(AppDbContext db, string entityType, string entityId, PartnerAgency agency, string? partnerAgentId,
        bool released, bool includesChildren, CancellationToken cancellationToken)
    {
        var row = await db.PartnerShares
            .FirstOrDefaultAsync(s => s.EntityType == entityType && s.EntityId == entityId && s.Agency == agency && s.PartnerAgentId == partnerAgentId, cancellationToken);
        if (released)
        {
            if (row is null)
            {
                db.PartnerShares.Add(new PartnerShare
                {
                    EntityType = entityType,
                    EntityId = entityId,
                    Agency = agency,
                    PartnerAgentId = partnerAgentId,
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
