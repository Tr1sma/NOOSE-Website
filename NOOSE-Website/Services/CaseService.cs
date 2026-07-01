using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Cases;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Cases;

namespace NOOSE_Website.Services;

public class CaseService(IDbContextFactory<AppDbContext> dbFactory, ICaseNumberService caseNumber, IProfileSuggestionService suggestion) : ICaseService
{
    public async Task<List<Case>> GetListAsync(ViewerScope scope, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await VisibleCases(db, scope)
            .OrderByDescending(v => v.ModifiedAt ?? v.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Case?> GetDetailAsync(string id, ViewerScope scope, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var @case = await db.Cases.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
        if (@case is null || !await Visibility.IsRecordVisibleAsync(db, nameof(Case), id, scope, cancellationToken))
        {
            return null;
        }
        return @case;
    }

    public async Task<List<Case>> GetTrashAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Cases.IgnoreQueryFilters()
            .Where(v => v.IsDeleted)
            .OrderByDescending(v => v.DeletedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<List<Case>> SearchAsync(string? searchText, bool isLeadership, int max = 20, CancellationToken cancellationToken = default)
        => SearchAsync(searchText, new ViewerScope(isLeadership, isLeadership, null, null), max, cancellationToken);

    public async Task<List<Case>> SearchAsync(string? searchText, ViewerScope scope, int max = 20, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var mayClassified = scope.MayClassifiedRead;
        var isTru = scope.IsTru;
        var isHrb = scope.IsHrb;
        // surface a restricted case only when the viewer's secrecy scope covers its level (TRU/HRB audience or leadership)
        var query = db.Cases.Where(v =>
            !v.IsClassified
            || mayClassified
            || (v.IsTRUClassified && isTru)
            || (!v.IsTRUClassified && v.IsHRBClassified && isHrb));

        var s = searchText?.Trim();
        if (!string.IsNullOrEmpty(s))
        {
            query = query.Where(v => v.Title.Contains(s) || v.CaseNumber.Contains(s));
        }

        return await query
            .OrderBy(v => v.Title)
            .Take(max)
            .ToListAsync(cancellationToken);
    }

    public async Task<Case> CreateAsync(CaseInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        ClassificationHelper.CheckRankGate(input.Classification, actor);
        Permission.RequireMayAssignClassification(actor, input.SecrecyLevel);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var isCompleted = CaseStatusDisplay.IsCompleted(input.Status);
        var @case = new Case
        {
            CaseNumber = await caseNumber.NextAsync(db, "V", cancellationToken),
            Title = input.Title.Trim(),
            Type = input.Type.TrimToNull(),
            Status = input.Status,
            Description = input.Description.TrimToNull(),
            Summary = input.Summary.TrimToNull(),
            ClosingNote = input.ClosingNote.TrimToNull(),
            CompletedAt = isCompleted ? DateTime.UtcNow : null,
            Classification = input.Classification,
            SecrecyLevel = input.SecrecyLevel,
        };

        if (input.Classification != Classification.Unknown)
        {
            db.ClassificationHistory.Add(ClassificationHelper.Entry(nameof(Case), @case.Id, input.Classification, input.ClassificationJustification, actor));
        }

        await SuggestionsStageAsync(db, @case.IsRestricted, input.Type, cancellationToken);

        db.Cases.Add(@case);
        await db.SaveChangesAsync(cancellationToken);

        // Auto-assign the creator as case lead so at least one always exists.
        var creatorId = actor.GetAgentId();
        if (creatorId is not null)
        {
            db.CaseAgents.Add(new CaseAgent
            {
                CaseId = @case.Id,
                AgentId = creatorId,
                IsCaseLead = true,
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
        return @case;
    }

    public async Task RefreshAsync(string id, CaseInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var @case = await db.Cases.FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Vorgang '{id}' nicht gefunden.");

        if (@case.IsClassified && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        // Track the completion timestamp with the status: set on close, clear when reopened.
        var wasCompleted = CaseStatusDisplay.IsCompleted(@case.Status);
        var isCompleted = CaseStatusDisplay.IsCompleted(input.Status);

        @case.Title = input.Title.Trim();
        @case.Type = input.Type.TrimToNull();
        @case.Status = input.Status;
        @case.Description = input.Description.TrimToNull();
        @case.Summary = input.Summary.TrimToNull();
        @case.ClosingNote = input.ClosingNote.TrimToNull();
        Permission.RequireMayAssignClassification(actor, input.SecrecyLevel);
        @case.SecrecyLevel = input.SecrecyLevel;

        if (isCompleted && !wasCompleted)
        {
            @case.CompletedAt = DateTime.UtcNow;
        }
        else if (!isCompleted)
        {
            @case.CompletedAt = null;
        }

        await SuggestionsStageAsync(db, @case.IsRestricted, input.Type, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var @case = await db.Cases.FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Vorgang '{id}' nicht gefunden.");
        db.Cases.Remove(@case);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var @case = await db.Cases.IgnoreQueryFilters()
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Vorgang '{id}' nicht gefunden.");

        @case.IsDeleted = false;
        @case.DeletedAt = null;
        @case.DeletedById = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ClassificationSetAsync(string id, Classification @new, string? justification, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        ClassificationHelper.CheckRankGate(@new, actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var @case = await db.Cases.FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Vorgang '{id}' nicht gefunden.");

        if (@case.IsClassified && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        @case.Classification = @new;
        db.ClassificationHistory.Add(ClassificationHelper.Entry(nameof(Case), id, @new, justification, actor));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<ClassificationHistory>> GetClassificationHistoryAsync(string id, ViewerScope scope, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await Visibility.IsRecordVisibleAsync(db, nameof(Case), id, scope, cancellationToken))
        {
            return new();
        }
        return await db.ClassificationHistory
            .Where(e => e.EntityType == nameof(Case) && e.EntityId == id)
            .OrderByDescending(e => e.Timestamp)
            .ToListAsync(cancellationToken);
    }

    private static IQueryable<Case> VisibleCases(AppDbContext db, ViewerScope scope)
        => scope.PartnerAgency is { } agency
            ? db.Cases.OnlyPartnerVisible(db, agency, scope.MeId)
            : db.Cases.Where(v =>
                (!v.IsClassified && !v.IsTRUClassified && !v.IsHRBClassified)
                || scope.MayClassifiedRead
                || (v.IsTRUClassified && scope.IsTru)
                || (v.IsHRBClassified && scope.IsHrb));

    public async Task<List<CaseAgent>> GetAgentsAsync(string caseId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.CaseAgents
            .Where(a => a.CaseId == caseId)
            .Include(a => a.Agent)
            .OrderByDescending(a => a.IsCaseLead)
            .ThenBy(a => a.Agent!.Codename)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<CaseAgent>> GetCaseLeadAsync(string caseId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.CaseAgents
            .Where(a => a.CaseId == caseId && a.IsCaseLead)
            .Include(a => a.Agent)
            .OrderBy(a => a.Agent!.Codename)
            .ToListAsync(cancellationToken);
    }

    public async Task AgentAllocateAsync(string caseId, string agentId, bool asCaseLead, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var @case = await db.Cases.FirstOrDefaultAsync(v => v.Id == caseId, cancellationToken)
            ?? throw new InvalidOperationException($"Vorgang '{caseId}' nicht gefunden.");
        if (@case.IsClassified && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }
        await RequireLeadershipOrFFAsync(db, caseId, actor, cancellationToken);
        // Only leadership may grant the case-lead flag.
        if (asCaseLead)
        {
            Permission.RequireLeadership(actor);
        }
        if (!await db.Users.AnyAsync(u => u.Id == agentId, cancellationToken))
        {
            throw new InvalidOperationException("Der gewählte Agent wurde nicht gefunden.");
        }
        if (await db.CaseAgents.AnyAsync(a => a.CaseId == caseId && a.AgentId == agentId, cancellationToken))
        {
            throw new InvalidOperationException("Dieser Agent ist dem Vorgang bereits zugeteilt.");
        }

        db.CaseAgents.Add(new CaseAgent
        {
            CaseId = caseId,
            AgentId = agentId,
            IsCaseLead = asCaseLead,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AgentRemoveAsync(string allocationId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var allocation = await db.CaseAgents.Include(a => a.Case).FirstOrDefaultAsync(a => a.Id == allocationId, cancellationToken);
        if (allocation is null)
        {
            return;
        }
        if (allocation.Case?.IsClassified == true && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }
        await RequireLeadershipOrFFAsync(db, allocation.CaseId, actor, cancellationToken);
        db.CaseAgents.Remove(allocation);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task CaseLeadSetAsync(string allocationId, bool @is, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        // Granting/revoking the case lead is leadership-only.
        Permission.RequireLeadership(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var allocation = await db.CaseAgents.FirstOrDefaultAsync(a => a.Id == allocationId, cancellationToken)
            ?? throw new InvalidOperationException("Zuteilung nicht gefunden.");
        allocation.IsCaseLead = @is;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Throws unless the actor is leadership or a case lead of this case.</summary>
    private static async Task RequireLeadershipOrFFAsync(AppDbContext db, string caseId, ClaimsPrincipal actor, CancellationToken cancellationToken)
    {
        if (actor.IsLeadership())
        {
            return;
        }
        var agentId = actor.GetAgentId();
        var isFF = agentId is not null && await db.CaseAgents
            .AnyAsync(a => a.CaseId == caseId && a.AgentId == agentId && a.IsCaseLead, cancellationToken);
        if (!isFF)
        {
            throw new UnauthorizedAccessException(
                "Agents zuteilen oder entfernen dürfen nur die Führung oder ein Fallführer dieser Akte.");
        }
    }

    public async Task<List<AuditLog>> GetHistoryAsync(string caseId, bool isLeadership, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await Visibility.IsRecordVisibleAsync(db, nameof(Case), caseId, isLeadership, cancellationToken))
        {
            return new();
        }
        var agentAllocationIds = await db.CaseAgents
            .Where(a => a.CaseId == caseId)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        // Manual links touching this case, including removed ones so their "removed" entry still shows.
        var relationIds = await db.Links
            .IgnoreQueryFilters()
            .Where(v => !v.Automatic
                && ((v.SourceType == nameof(Case) && v.SourceId == caseId)
                 || (v.TargetType == nameof(Case) && v.TargetId == caseId)))
            .Select(v => v.Id)
            .ToListAsync(cancellationToken);

        var ids = new HashSet<string> { caseId };
        ids.UnionWith(agentAllocationIds);
        ids.UnionWith(relationIds);
        var types = new[] { nameof(Case), nameof(CaseAgent), nameof(Link) };

        return await db.AuditLogs
            .Where(a => types.Contains(a.EntityType) && ids.Contains(a.EntityId))
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Stages the case type into the shared suggestion catalog; classified values are excluded, staged within the caller's SaveChanges.</summary>
    private async Task SuggestionsStageAsync(AppDbContext db, bool isClassified, string? type, CancellationToken cancellationToken)
    {
        if (isClassified || string.IsNullOrWhiteSpace(type))
        {
            return;
        }
        await suggestion.StageAsync(db, SuggestionType.CaseType, new[] { type }, cancellationToken);
    }
}
