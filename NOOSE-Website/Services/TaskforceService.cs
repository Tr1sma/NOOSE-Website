using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Taskforces;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="ITaskforceService" />
public class TaskforceService(IDbContextFactory<AppDbContext> dbFactory, ICaseNumberService caseNumber) : ITaskforceService
{
    public async Task<List<Taskforce>> GetListAsync(bool mayAll, string? meId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Taskforces
            .OnlyVisible(db, mayAll, meId)
            .OrderByDescending(t => t.ModifiedAt ?? t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Taskforce?> GetDetailAsync(string id, bool mayAll, string? meId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var taskforce = await db.Taskforces.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (taskforce is null)
        {
            return null;
        }
        // Sichtbar nur für Führung/Admin oder zugeteilte Agenten (Verschlusssache ist damit subsumiert).
        if (!mayAll
            && !(meId is not null && await db.TaskforceAgents.AnyAsync(a => a.TaskforceId == id && a.AgentId == meId, cancellationToken)))
        {
            return null;
        }
        return taskforce;
    }

    public async Task<List<Taskforce>> GetTrashAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Taskforces.IgnoreQueryFilters()
            .Where(t => t.IsDeleted)
            .OrderByDescending(t => t.DeletedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Taskforce>> SearchAsync(string? searchText, bool mayAll, string? meId, int max = 20, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Taskforces.OnlyVisible(db, mayAll, meId);

        var s = searchText?.Trim();
        if (!string.IsNullOrEmpty(s))
        {
            query = query.Where(t => t.Name.Contains(s) || t.CaseNumber.Contains(s));
        }

        return await query
            .OrderBy(t => t.Name)
            .Take(max)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Taskforce>> GetRequestedAsync(CancellationToken cancellationToken = default)
    {
        // Nur für den Führungs-Freigabe-Posteingang (Seite ist Policies.Fuehrung-gated) → kein VS-Filter nötig.
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Taskforces
            .Where(t => t.Status == TaskforceStatus.Requested)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Taskforce> CreateAsync(TaskforceInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var taskforce = new Taskforce
        {
            CaseNumber = await caseNumber.NextAsync(db, "TF", cancellationToken),
            Name = input.Name.Trim(),
            Purpose = input.Purpose.TrimToNull(),
            Scope = input.Scope,
            Status = TaskforceStatus.Requested,
            Remarks = input.Remarks.TrimToNull(),
            IsClassified = input.IsClassified,
        };

        db.Taskforces.Add(taskforce);
        await db.SaveChangesAsync(cancellationToken);

        // Ersteller automatisch als Chefermittler (Leitung) zuteilen (so existiert stets mindestens eine Leitung).
        var creatorId = actor.GetAgentId();
        if (creatorId is not null)
        {
            db.TaskforceAgents.Add(new TaskforceAgent
            {
                TaskforceId = taskforce.Id,
                AgentId = creatorId,
                Role = TaskforceRole.LeadInvestigator,
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
        return taskforce;
    }

    public async Task RefreshAsync(string id, TaskforceInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var taskforce = await db.Taskforces.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Taskforce '{id}' nicht gefunden.");

        if (taskforce.IsClassified && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        taskforce.Name = input.Name.Trim();
        taskforce.Purpose = input.Purpose.TrimToNull();
        taskforce.Scope = input.Scope;
        taskforce.Remarks = input.Remarks.TrimToNull();
        taskforce.IsClassified = input.IsClassified;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var taskforce = await db.Taskforces.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Taskforce '{id}' nicht gefunden.");
        db.Taskforces.Remove(taskforce);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var taskforce = await db.Taskforces.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Taskforce '{id}' nicht gefunden.");

        taskforce.IsDeleted = false;
        taskforce.DeletedAt = null;
        taskforce.DeletedById = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ApprovalSetAsync(string id, TaskforceStatus @new, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        // Genehmigen/Ablehnen/Auflösen ist der Führung vorbehalten (Plan §6 „Taskforce genehmigen").
        Permission.RequireLeadership(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var taskforce = await db.Taskforces.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Taskforce '{id}' nicht gefunden.");

        taskforce.Status = @new;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<TaskforceAgent>> GetAgentsAsync(string taskforceId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.TaskforceAgents
            .Where(a => a.TaskforceId == taskforceId)
            .Include(a => a.Agent)
            // Leitung (Rolle != Mitglied) zuerst, dann nach Rolle (Chefermittler < CID-Lead < TRU-Lead), dann Codename.
            .OrderBy(a => a.Role == TaskforceRole.Member)
            .ThenBy(a => a.Role)
            .ThenBy(a => a.Agent!.Codename)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<TaskforceAgent>> GetLeadAsync(string taskforceId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.TaskforceAgents
            .Where(a => a.TaskforceId == taskforceId && a.Role != TaskforceRole.Member)
            .Include(a => a.Agent)
            .OrderBy(a => a.Role)
            .ThenBy(a => a.Agent!.Codename)
            .ToListAsync(cancellationToken);
    }

    public async Task AgentAllocateAsync(string taskforceId, string agentId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var taskforce = await db.Taskforces.FirstOrDefaultAsync(t => t.Id == taskforceId, cancellationToken)
            ?? throw new InvalidOperationException($"Taskforce '{taskforceId}' nicht gefunden.");
        if (taskforce.IsClassified && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }
        await RequireLeadershipOrLeadAsync(db, taskforceId, actor, cancellationToken);
        if (!await db.Users.AnyAsync(u => u.Id == agentId, cancellationToken))
        {
            throw new InvalidOperationException("Der gewählte Agent wurde nicht gefunden.");
        }
        if (await db.TaskforceAgents.AnyAsync(a => a.TaskforceId == taskforceId && a.AgentId == agentId, cancellationToken))
        {
            throw new InvalidOperationException("Dieser Agent ist der Taskforce bereits zugeteilt.");
        }

        db.TaskforceAgents.Add(new TaskforceAgent
        {
            TaskforceId = taskforceId,
            AgentId = agentId,
            Role = TaskforceRole.Member,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AgentRemoveAsync(string allocationId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var allocation = await db.TaskforceAgents.Include(a => a.Taskforce).FirstOrDefaultAsync(a => a.Id == allocationId, cancellationToken);
        if (allocation is null)
        {
            return;
        }
        if (allocation.Taskforce?.IsClassified == true && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }
        await RequireLeadershipOrLeadAsync(db, allocation.TaskforceId, actor, cancellationToken);
        db.TaskforceAgents.Remove(allocation);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RoleSetAsync(string allocationId, TaskforceRole role, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        // Rollen/Leitung vergeben/entziehen ist der Führung vorbehalten.
        Permission.RequireLeadership(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var allocation = await db.TaskforceAgents.FirstOrDefaultAsync(a => a.Id == allocationId, cancellationToken)
            ?? throw new InvalidOperationException("Zuteilung nicht gefunden.");
        allocation.Role = role;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Wirft, wenn der Handelnde weder Führung noch Leitung (Rolle != Mitglied) dieser Taskforce ist.</summary>
    private static async Task RequireLeadershipOrLeadAsync(AppDbContext db, string taskforceId, ClaimsPrincipal actor, CancellationToken cancellationToken)
    {
        if (actor.IsLeadership())
        {
            return;
        }
        var agentId = actor.GetAgentId();
        var isLead = agentId is not null && await db.TaskforceAgents
            .AnyAsync(a => a.TaskforceId == taskforceId && a.AgentId == agentId && a.Role != TaskforceRole.Member, cancellationToken);
        if (!isLead)
        {
            throw new UnauthorizedAccessException(
                "Agents zuteilen oder entfernen dürfen nur die Führung oder die Leitung dieser Taskforce.");
        }
    }

    public async Task<List<AuditLog>> GetHistoryAsync(string taskforceId, bool mayAll, string? meId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await Visibility.IsRecordVisibleAsync(db, nameof(Taskforce), taskforceId, mayAll, cancellationToken, meId))
        {
            return new();
        }
        var agentAllocationIds = await db.TaskforceAgents
            .Where(a => a.TaskforceId == taskforceId)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        // Manuelle Beziehungen (Verknüpfungen), die diese Taskforce als Quelle oder Ziel berühren –
        // inkl. bereits entfernter (IgnoreQueryFilters), damit auch deren „entfernt"-Eintrag erscheint.
        var relationIds = await db.Links
            .IgnoreQueryFilters()
            .Where(v => !v.Automatic
                && ((v.SourceType == nameof(Taskforce) && v.SourceId == taskforceId)
                 || (v.TargetType == nameof(Taskforce) && v.TargetId == taskforceId)))
            .Select(v => v.Id)
            .ToListAsync(cancellationToken);

        var ids = new HashSet<string> { taskforceId };
        ids.UnionWith(agentAllocationIds);
        ids.UnionWith(relationIds);
        var types = new[] { nameof(Taskforce), nameof(TaskforceAgent), nameof(Link) };

        return await db.AuditLogs
            .Where(a => types.Contains(a.EntityType) && ids.Contains(a.EntityId))
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync(cancellationToken);
    }
}
