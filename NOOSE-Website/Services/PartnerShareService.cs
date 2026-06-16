using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Cases;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.Groups;
using NOOSE_Website.Data.Entities.Operations;
using NOOSE_Website.Data.Entities.Parties;
using NOOSE_Website.Data.Entities.People;
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
        // order on the real column before projecting; ordering by the record's property is untranslatable
        var rows = await db.PartnerShares.AsNoTracking()
            .Where(s => s.EntityType == entityType && s.EntityId == entityId && s.PartnerAgentId != null)
            .Join(db.Users.AsNoTracking(), s => s.PartnerAgentId, a => a.Id, (s, a) => new { s, a })
            .OrderBy(x => x.a.Codename)
            .Select(x => new PartnerIndividualShareState(x.a.Id, x.a.Codename, x.s.Agency, x.a.PartnerRank, x.s.IncludesChildren))
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

    public async Task<IReadOnlyList<PartnerTypeShareSummary>> GetTypeSummariesAsync(PartnerAgency agency, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // agency-wide shares only; one active row per (type, record, agency) so a row count = record count
        var sharedCounts = (await db.PartnerShares
            .Where(s => s.Agency == agency && s.PartnerAgentId == null)
            .GroupBy(s => s.EntityType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken))
            .ToDictionary(x => x.Type, x => x.Count);

        var result = new List<PartnerTypeShareSummary>();
        foreach (var type in PartnerTabCatalog.All)
        {
            var total = await CountRecordsAsync(db, type.TypeKey, cancellationToken);
            result.Add(new PartnerTypeShareSummary(type.TypeKey, type.DisplayName, total, sharedCounts.GetValueOrDefault(type.TypeKey)));
        }
        return result;
    }

    public async Task<int> SetTypeAsync(string entityType, PartnerAgency agency, bool released, bool includesChildren,
        ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);
        if (!PartnerVisibility.IsReleasableType(entityType))
        {
            throw new InvalidOperationException("Dieser Akten-Typ kann nicht freigegeben werden.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        if (!released)
        {
            // withdraw every agency-wide share of this type; individual-account shares stay
            var rows = await db.PartnerShares
                .Where(s => s.EntityType == entityType && s.Agency == agency && s.PartnerAgentId == null)
                .ToListAsync(cancellationToken);
            db.PartnerShares.RemoveRange(rows);
            await db.SaveChangesAsync(cancellationToken);
            return rows.Count;
        }

        var allIds = await RecordIdsAsync(db, entityType, cancellationToken);
        var existing = await db.PartnerShares
            .Where(s => s.EntityType == entityType && s.Agency == agency && s.PartnerAgentId == null)
            .ToListAsync(cancellationToken);
        var existingById = existing.ToDictionary(s => s.EntityId);

        var added = 0;
        foreach (var id in allIds)
        {
            if (existingById.TryGetValue(id, out var row))
            {
                row.IncludesChildren = includesChildren;
            }
            else
            {
                db.PartnerShares.Add(new PartnerShare
                {
                    EntityType = entityType,
                    EntityId = id,
                    Agency = agency,
                    PartnerAgentId = null,
                    IncludesChildren = includesChildren,
                });
                added++;
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        return added;
    }

    // live record ids per type (soft-deleted rows excluded by the global query filter)
    private static async Task<List<string>> RecordIdsAsync(AppDbContext db, string entityType, CancellationToken cancellationToken)
        => entityType switch
        {
            nameof(Person) => await db.People.Select(p => p.Id).ToListAsync(cancellationToken),
            nameof(Faction) => await db.Factions.Select(f => f.Id).ToListAsync(cancellationToken),
            nameof(PersonGroup) => await db.PersonGroups.Select(g => g.Id).ToListAsync(cancellationToken),
            nameof(Party) => await db.Parties.Select(p => p.Id).ToListAsync(cancellationToken),
            nameof(Operation) => await db.Operations.Select(o => o.Id).ToListAsync(cancellationToken),
            nameof(Case) => await db.Cases.Select(c => c.Id).ToListAsync(cancellationToken),
            nameof(Document) => await db.Documents.Select(d => d.Id).ToListAsync(cancellationToken),
            nameof(Law) => await db.Laws.Select(l => l.Id).ToListAsync(cancellationToken),
            _ => new List<string>(),
        };

    private static async Task<int> CountRecordsAsync(AppDbContext db, string entityType, CancellationToken cancellationToken)
        => entityType switch
        {
            nameof(Person) => await db.People.CountAsync(cancellationToken),
            nameof(Faction) => await db.Factions.CountAsync(cancellationToken),
            nameof(PersonGroup) => await db.PersonGroups.CountAsync(cancellationToken),
            nameof(Party) => await db.Parties.CountAsync(cancellationToken),
            nameof(Operation) => await db.Operations.CountAsync(cancellationToken),
            nameof(Case) => await db.Cases.CountAsync(cancellationToken),
            nameof(Document) => await db.Documents.CountAsync(cancellationToken),
            nameof(Law) => await db.Laws.CountAsync(cancellationToken),
            _ => 0,
        };

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
