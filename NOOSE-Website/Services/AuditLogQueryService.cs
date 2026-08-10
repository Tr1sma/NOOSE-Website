using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Common;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IAuditLogQueryService" />
public class AuditLogQueryService(IDbContextFactory<AppDbContext> dbFactory) : IAuditLogQueryService
{
    public async Task<AuditLogPage> QueryChangesAsync(AuditLogFilter filter, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireClassifiedRead(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var q = db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.AgentId))
        {
            q = filter.AgentId switch
            {
                AuditAgentGroup.TeamLeads => q.Where(x => db.Users.Where(u => u.IsTeamLead)
                    .Select(u => u.Id).Contains(x.AgentId)),
                AuditAgentGroup.Partners => q.Where(x => db.Users.Where(u => u.PartnerAgency != null)
                    .Select(u => u.Id).Contains(x.AgentId)),
                _ => q.Where(x => x.AgentId == filter.AgentId),
            };
        }
        if (!string.IsNullOrWhiteSpace(filter.EntityType))
        {
            q = q.Where(x => x.EntityType == filter.EntityType);
        }
        if (filter.Action is { } action)
        {
            q = q.Where(x => x.Action == action);
        }
        if (!string.IsNullOrWhiteSpace(filter.EntityId))
        {
            q = q.Where(x => x.EntityId == filter.EntityId);
        }
        if (filter.FromUtc is { } from)
        {
            q = q.Where(x => x.Timestamp >= from);
        }
        if (filter.ToUtc is { } to)
        {
            q = q.Where(x => x.Timestamp < to);
        }

        var total = await q.CountAsync(cancellationToken);
        var rows = await q.OrderByDescending(x => x.Timestamp).Take(AuditLogFilter.MaxRows)
            .Select(x => new AuditChangeRow(x.Id, x.Timestamp, x.AgentName, x.EntityType, x.EntityId, x.Action, x.ChangesJson))
            .ToListAsync(cancellationToken);

        return new AuditLogPage(rows, total);
    }

    public async Task<AccessLogPage> QueryAccessAsync(AuditLogFilter filter, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireClassifiedRead(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var q = db.AccessLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.AgentId))
        {
            q = filter.AgentId switch
            {
                AuditAgentGroup.TeamLeads => q.Where(x => db.Users.Where(u => u.IsTeamLead)
                    .Select(u => u.Id).Contains(x.AgentId)),
                AuditAgentGroup.Partners => q.Where(x => db.Users.Where(u => u.PartnerAgency != null)
                    .Select(u => u.Id).Contains(x.AgentId)),
                _ => q.Where(x => x.AgentId == filter.AgentId),
            };
        }
        if (!string.IsNullOrWhiteSpace(filter.EntityType))
        {
            q = q.Where(x => x.EntityType == filter.EntityType);
        }
        if (!string.IsNullOrWhiteSpace(filter.EntityId))
        {
            q = q.Where(x => x.EntityId == filter.EntityId);
        }
        if (filter.FromUtc is { } from)
        {
            q = q.Where(x => x.Timestamp >= from);
        }
        if (filter.ToUtc is { } to)
        {
            q = q.Where(x => x.Timestamp < to);
        }

        var total = await q.CountAsync(cancellationToken);
        var rows = await q.OrderByDescending(x => x.Timestamp).Take(AuditLogFilter.MaxRows)
            .Select(x => new AuditAccessRow(x.Id, x.Timestamp, x.AgentName, x.EntityType, x.EntityId))
            .ToListAsync(cancellationToken);

        return new AccessLogPage(rows, total);
    }

    public async Task<AuditFilterOptions> GetFilterOptionsAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireClassifiedRead(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var agents = new List<AuditAgentOption>();

        // whole-group pseudo filters first, each only while such accounts exist
        if (await db.Users.AnyAsync(u => u.IsTeamLead, cancellationToken))
        {
            agents.Add(new AuditAgentOption(AuditAgentGroup.TeamLeads, "Alle Teamleitungen"));
        }
        if (await db.Users.AnyAsync(u => u.PartnerAgency != null, cancellationToken))
        {
            agents.Add(new AuditAgentOption(AuditAgentGroup.Partners, "Alle Partner"));
        }

        agents.AddRange((await AgentDirectory.AllForAuditAsync(db, cancellationToken))
            .Select(a => new AuditAgentOption(a.Id, a.Codename, RoleDetail(a.IsSupervision, a.Agency))));

        // union of types that actually appear in either log
        var changeTypes = await db.AuditLogs.AsNoTracking().Select(x => x.EntityType).Distinct().ToListAsync(cancellationToken);
        var accessTypes = await db.AccessLogs.AsNoTracking().Select(x => x.EntityType).Distinct().ToListAsync(cancellationToken);
        var types = changeTypes.Union(accessTypes).OrderBy(t => t).ToList();

        return new AuditFilterOptions(agents, types);
    }

    private static string? RoleDetail(bool isSupervision, PartnerAgency? agency)
    {
        if (agency is { } partnerAgency)
        {
            return $"Partner · {PartnerAgencyDisplay.Name(partnerAgency)}";
        }
        return isSupervision ? "Teamleitung" : null;
    }
}
