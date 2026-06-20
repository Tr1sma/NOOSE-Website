using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Infrastructure.Shares;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

public class DocumentAccessService(IDbContextFactory<AppDbContext> dbFactory, DocumentAccessBroadcaster broadcaster)
    : IDocumentAccessService
{
    private const Rank LeadershipFloor = Rank.SupervisorySpecialAgent;

    public async Task<IReadOnlyList<DocumentAccessEntry>> GetAccessListAsync(string documentId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var doc = await db.Documents.AsNoTracking()
            .Where(d => d.Id == documentId)
            .Select(d => new { d.IsClassified, d.IsTRUClassified, d.IsHRBClassified, d.OwnerTaskforceId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Dokument nicht gefunden.");

        // internal, active agents only
        var q = db.Users.AsNoTracking().Where(a => a.PartnerAgency == null && a.Status == AgentStatus.Active);

        // classification gate, replicated from DocumentViewerScope against stored fields:
        // mayClassified(a) = leadership(a) || onlyReader(a) = (IsAdmin || Rank>=floor) || (IsTeamLead && !IsAdmin)
        if (doc.IsClassified)
            q = q.Where(a => a.IsAdmin || a.Rank >= LeadershipFloor || (a.IsTeamLead && !a.IsAdmin));
        else if (doc.IsTRUClassified)
            q = q.Where(a => a.IsAdmin || a.Rank >= LeadershipFloor || (a.IsTeamLead && !a.IsAdmin) || a.IsTRU);
        else if (doc.IsHRBClassified)
            q = q.Where(a => a.IsAdmin || a.Rank >= LeadershipFloor || (a.IsTeamLead && !a.IsAdmin) || a.IsHRB);

        // taskforce-internal: members and leadership/admin only
        if (doc.OwnerTaskforceId is { } tfId)
            q = q.Where(a => a.IsAdmin || a.Rank >= LeadershipFloor
                || db.TaskforceAgents.Any(ta => ta.TaskforceId == tfId && ta.AgentId == a.Id));

        var agents = await q
            .OrderBy(a => a.Codename)
            .Select(a => new { a.Id, a.Codename, a.RealName, a.Rank })
            .ToListAsync(cancellationToken);

        var excluded = (await db.DocumentAccessExclusions.AsNoTracking()
                .Where(x => x.DocumentId == documentId)
                .Select(x => x.AgentId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        return agents
            .Select(a => new DocumentAccessEntry(a.Id, a.Codename, a.RealName, a.Rank, excluded.Contains(a.Id)))
            .ToList();
    }

    public async Task RevokeAsync(string documentId, string agentId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);
        Permission.RequireWriteAccess(actor);

        if (agentId == actor.GetAgentId())
            throw new InvalidOperationException("Du kannst dir den Zugriff nicht selbst entziehen.");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var target = await db.Users.AsNoTracking()
            .Where(a => a.Id == agentId)
            .Select(a => new { a.IsAdmin, a.PartnerAgency })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Agent nicht gefunden.");
        if (target.IsAdmin)
            throw new InvalidOperationException("Einem Admin kann der Zugriff nicht entzogen werden.");
        if (target.PartnerAgency != null)
            throw new InvalidOperationException("Nur interne Agenten stehen auf der Einsichtsliste.");

        var existing = await db.DocumentAccessExclusions
            .FirstOrDefaultAsync(x => x.DocumentId == documentId && x.AgentId == agentId, cancellationToken);
        if (existing is null)
        {
            db.DocumentAccessExclusions.Add(new DocumentAccessExclusion { DocumentId = documentId, AgentId = agentId });
            await db.SaveChangesAsync(cancellationToken);
        }
        broadcaster.Report(documentId);
    }

    public async Task RestoreAsync(string documentId, string agentId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);
        Permission.RequireWriteAccess(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.DocumentAccessExclusions
            .FirstOrDefaultAsync(x => x.DocumentId == documentId && x.AgentId == agentId, cancellationToken);
        if (row is not null)
        {
            db.DocumentAccessExclusions.Remove(row); // soft-delete via interceptor = restore
            await db.SaveChangesAsync(cancellationToken);
        }
        broadcaster.Report(documentId);
    }
}
