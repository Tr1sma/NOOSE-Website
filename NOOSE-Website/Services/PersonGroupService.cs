using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Groups;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Groups;
using NOOSE_Website.Models.People;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IPersonengruppeService" />
public class PersonGroupService(IDbContextFactory<AppDbContext> dbFactory, ICaseNumberService caseNumber, IPersonService personService, IThreatScoreService threat) : IPersonGroupService
{
    public async Task<List<PersonGroup>> GetListAsync(ViewerScope scope, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // include members so the list count matches the detail view
        return await VisiblePersonGroups(db, scope)
            .Include(g => g.Members).ThenInclude(m => m.Person)
            .OrderByDescending(g => g.ModifiedAt ?? g.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<PersonGroup?> GetDetailAsync(string id, ViewerScope scope, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var group = await db.PersonGroups.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (group is null || !await Visibility.IsRecordVisibleAsync(db, nameof(PersonGroup), id, scope, cancellationToken))
        {
            return null;
        }
        return group;
    }

    public async Task<List<PersonGroup>> GetTrashAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.PersonGroups.IgnoreQueryFilters()
            .Where(g => g.IsDeleted)
            .OrderByDescending(g => g.DeletedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<PersonGroup>> SearchAsync(string? searchText, bool isLeadership, int max = 20, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.PersonGroups.Where(g => isLeadership || !g.IsClassified);

        var s = searchText?.Trim();
        if (!string.IsNullOrEmpty(s))
        {
            query = query.Where(g => g.Name.Contains(s) || g.CaseNumber.Contains(s));
        }

        return await query
            .OrderBy(g => g.Name)
            .Take(max)
            .ToListAsync(cancellationToken);
    }

    public async Task<PersonGroup> CreateAsync(PersonGroupInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        ClassificationHelper.CheckRankGate(input.Classification, actor);
        Permission.RequireMayAssignClassification(actor, input.SecrecyLevel);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var group = new PersonGroup
        {
            CaseNumber = await caseNumber.NextAsync(db, "G", cancellationToken),
            Name = input.Name.Trim(),
            Description = input.Description.TrimToNull(),
            Targets = input.Targets.TrimToNull(),
            Kind = input.Kind,
            Classification = input.Classification,
            EstimatedMemberCount = input.EstimatedMemberCount,
            SecrecyLevel = input.SecrecyLevel,
        };

        if (input.Classification != Classification.Unknown)
        {
            db.ClassificationHistory.Add(ClassificationHelper.Entry(nameof(PersonGroup), group.Id, input.Classification, input.ClassificationJustification, actor));
        }

        db.PersonGroups.Add(group);
        await db.SaveChangesAsync(cancellationToken);

        // import members from the create form, then build colleague links
        if (input.Members.Count > 0)
        {
            var existingIds = input.Members
                .Select(m => m.PersonId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();
            var existing = existingIds.Count == 0
                ? new HashSet<string>()
                : (await db.People.Where(p => existingIds.Contains(p.Id)).Select(p => p.Id)
                    .ToListAsync(cancellationToken)).ToHashSet();

            var added = new List<string>();
            var seen = new HashSet<string>();
            var seenNewNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in input.Members)
            {
                string? pid = null;
                if (!string.IsNullOrWhiteSpace(m.PersonId) && existing.Contains(m.PersonId))
                {
                    pid = m.PersonId;
                }
                else if (string.IsNullOrWhiteSpace(m.PersonId) && !string.IsNullOrWhiteSpace(m.NewPersonName))
                {
                    // same new name within one form creates only one record
                    if (!seenNewNames.Add(m.NewPersonName.Trim()))
                    {
                        continue;
                    }
                    var person = await personService.CreateAsync(new PersonInput { Name = m.NewPersonName.Trim() }, actor, cancellationToken);
                    pid = person.Id;
                }
                if (pid is null || !seen.Add(pid))
                {
                    continue;
                }
                db.PersonGroupMembers.Add(new PersonGroupMember
                {
                    PersonGroupId = group.Id,
                    PersonId = pid,
                    Role = m.Role.TrimToNull(),
                    IsLead = m.IsLead,
                });
                added.Add(pid);
            }
            if (added.Count > 0)
            {
                await db.SaveChangesAsync(cancellationToken);
                foreach (var pid in added)
                {
                    await GroupColleaguesSyncAsync(db, pid, cancellationToken);
                }
            }
        }

        // creator auto-assigned as investigation lead (ensures at least one)
        var creatorId = actor.GetAgentId();
        if (creatorId is not null)
        {
            db.PersonGroupAgents.Add(new PersonGroupAgent
            {
                PersonGroupId = group.Id,
                AgentId = creatorId,
                IsInvestigationLead = true,
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
        return group;
    }

    public async Task RefreshAsync(string id, PersonGroupInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var group = await db.PersonGroups.FirstOrDefaultAsync(g => g.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Personengruppe '{id}' nicht gefunden.");

        if (group.IsClassified && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        group.Name = input.Name.Trim();
        group.Description = input.Description.TrimToNull();
        group.Targets = input.Targets.TrimToNull();
        group.Kind = input.Kind;
        group.EstimatedMemberCount = input.EstimatedMemberCount;
        Permission.RequireMayAssignClassification(actor, input.SecrecyLevel);
        group.SecrecyLevel = input.SecrecyLevel;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var group = await db.PersonGroups.FirstOrDefaultAsync(g => g.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Personengruppe '{id}' nicht gefunden.");
        db.PersonGroups.Remove(group);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var group = await db.PersonGroups.IgnoreQueryFilters()
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Personengruppe '{id}' nicht gefunden.");

        group.IsDeleted = false;
        group.DeletedAt = null;
        group.DeletedById = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ClassificationSetAsync(string id, Classification @new, string? justification, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        ClassificationHelper.CheckRankGate(@new, actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var group = await db.PersonGroups.FirstOrDefaultAsync(g => g.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Personengruppe '{id}' nicht gefunden.");

        if (group.IsClassified && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        group.Classification = @new;
        db.ClassificationHistory.Add(ClassificationHelper.Entry(nameof(PersonGroup), id, @new, justification, actor));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<ClassificationHistory>> GetClassificationHistoryAsync(string id, ViewerScope scope, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await Visibility.IsRecordVisibleAsync(db, nameof(PersonGroup), id, scope, cancellationToken))
        {
            return new();
        }
        return await db.ClassificationHistory
            .Where(e => e.EntityType == nameof(PersonGroup) && e.EntityId == id)
            .OrderByDescending(e => e.Timestamp)
            .ToListAsync(cancellationToken);
    }

    // scope-filtered group query
    private static IQueryable<PersonGroup> VisiblePersonGroups(AppDbContext db, ViewerScope scope)
        => scope.PartnerAgency is { } agency
            ? db.PersonGroups.OnlyPartnerVisible(db, agency, scope.MeId)
            : db.PersonGroups.Where(g =>
                !g.IsClassified
                || scope.MayClassifiedRead
                || (g.IsTRUClassified && scope.IsTru)
                || (g.IsHRBClassified && scope.IsHrb));

    public async Task<List<PersonGroupMember>> GetMembersAsync(string groupId, ViewerScope scope, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var members = await db.PersonGroupMembers
            .Where(m => m.PersonGroupId == groupId)
            .Include(m => m.Person)
            .ToListAsync(cancellationToken);

        // null person = trashed; classified persons leadership-only
        var visible = members
            .Where(m => m.Person is not null && (scope.MayClassifiedRead || !m.Person.IsClassified))
            .ToList();
        if (scope.PartnerAgency is { } agency)
        {
            // partners: only members whose person is released
            var released = await PartnerVisibility.ReleasedParentIdsAsync(db, nameof(Person),
                visible.Select(m => m.PersonId).Distinct().ToList(), agency, scope.MeId, cancellationToken);
            visible = visible.Where(m => released.Contains(m.PersonId)).ToList();
        }
        return visible
            .OrderByDescending(m => m.IsLead)
            .ThenBy(m => m.Person!.Name)
            .ToList();
    }

    public async Task MemberAddAsync(string groupId, GroupMemberInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var group = await db.PersonGroups.FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken)
            ?? throw new InvalidOperationException($"Personengruppe '{groupId}' nicht gefunden.");
        if (group.IsClassified && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        var personId = await PersonIdDetermineAsync(db, input.PersonId, input.NewPersonName, actor, cancellationToken);
        if (await db.PersonGroupMembers.AnyAsync(m => m.PersonGroupId == groupId && m.PersonId == personId, cancellationToken))
        {
            throw new InvalidOperationException("Diese Person ist bereits Mitglied der Gruppe.");
        }

        // membership + colleague links in one transaction
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        db.PersonGroupMembers.Add(new PersonGroupMember
        {
            PersonGroupId = groupId,
            PersonId = personId,
            Role = input.Role.TrimToNull(),
            IsLead = input.IsLead,
        });
        await db.SaveChangesAsync(cancellationToken);
        await GroupColleaguesSyncAsync(db, personId, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        // membership/lead role affects the person score
        await threat.NewCalculatePersonScoreAsync(personId, cancellationToken);
    }

    /// <summary>Resolves an existing person id, or creates a fresh record when only a new name is given.</summary>
    private Task<string> PersonIdDetermineAsync(AppDbContext db, string? personId, string? newName, ClaimsPrincipal actor, CancellationToken cancellationToken)
        => MemberHelper.PersonIdDetermineAsync(db, personService, personId, newName, actor, cancellationToken);

    public async Task MemberChangeAsync(string memberId, string? role, bool isLead, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var member = await db.PersonGroupMembers.Include(m => m.PersonGroup).FirstOrDefaultAsync(m => m.Id == memberId, cancellationToken)
            ?? throw new InvalidOperationException("Mitgliedschaft nicht gefunden.");
        if (member.PersonGroup?.IsClassified == true && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }
        member.Role = role.TrimToNull();
        member.IsLead = isLead;
        await db.SaveChangesAsync(cancellationToken);
        await threat.NewCalculatePersonScoreAsync(member.PersonId, cancellationToken);
    }

    public async Task MemberRemoveAsync(string memberId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var member = await db.PersonGroupMembers.Include(m => m.PersonGroup).FirstOrDefaultAsync(m => m.Id == memberId, cancellationToken);
        if (member is null)
        {
            return;
        }
        if (member.PersonGroup?.IsClassified == true && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }
        var personId = member.PersonId;
        // soft-delete keeps the membership as a history entry (exit date)
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        db.PersonGroupMembers.Remove(member);
        await db.SaveChangesAsync(cancellationToken);
        await GroupColleaguesSyncAsync(db, personId, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        await threat.NewCalculatePersonScoreAsync(personId, cancellationToken);
    }

    public async Task<List<PersonGroupAgent>> GetAgentsAsync(string groupId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.PersonGroupAgents
            .Where(a => a.PersonGroupId == groupId)
            .Include(a => a.Agent)
            .OrderByDescending(a => a.IsInvestigationLead)
            .ThenBy(a => a.Agent!.Codename)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<PersonGroupAgent>> GetInvestigationLeadAsync(string groupId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.PersonGroupAgents
            .Where(a => a.PersonGroupId == groupId && a.IsInvestigationLead)
            .Include(a => a.Agent)
            .OrderBy(a => a.Agent!.Codename)
            .ToListAsync(cancellationToken);
    }

    public async Task AgentAllocateAsync(string groupId, string agentId, bool asInvestigationLead, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var group = await db.PersonGroups.FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken)
            ?? throw new InvalidOperationException($"Personengruppe '{groupId}' nicht gefunden.");
        if (group.IsClassified && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }
        await RequireLeadershipOrELAsync(db, groupId, actor, cancellationToken);
        // only leadership may grant the lead flag
        if (asInvestigationLead)
        {
            Permission.RequireLeadership(actor);
        }
        if (!await db.Users.AnyAsync(u => u.Id == agentId, cancellationToken))
        {
            throw new InvalidOperationException("Der gewählte Agent wurde nicht gefunden.");
        }
        if (await db.PersonGroupAgents.AnyAsync(a => a.PersonGroupId == groupId && a.AgentId == agentId, cancellationToken))
        {
            throw new InvalidOperationException("Dieser Agent ist der Gruppe bereits zugeteilt.");
        }

        db.PersonGroupAgents.Add(new PersonGroupAgent
        {
            PersonGroupId = groupId,
            AgentId = agentId,
            IsInvestigationLead = asInvestigationLead,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AgentRemoveAsync(string allocationId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var allocation = await db.PersonGroupAgents.Include(a => a.PersonGroup).FirstOrDefaultAsync(a => a.Id == allocationId, cancellationToken);
        if (allocation is null)
        {
            return;
        }
        if (allocation.PersonGroup?.IsClassified == true && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }
        await RequireLeadershipOrELAsync(db, allocation.PersonGroupId, actor, cancellationToken);
        db.PersonGroupAgents.Remove(allocation);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task InvestigationLeadSetAsync(string allocationId, bool @is, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        // leadership-only
        Permission.RequireLeadership(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var allocation = await db.PersonGroupAgents.FirstOrDefaultAsync(a => a.Id == allocationId, cancellationToken)
            ?? throw new InvalidOperationException("Zuteilung nicht gefunden.");
        allocation.IsInvestigationLead = @is;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Throws unless the actor is leadership or an investigation lead of this group.</summary>
    private static async Task RequireLeadershipOrELAsync(AppDbContext db, string groupId, ClaimsPrincipal actor, CancellationToken cancellationToken)
    {
        if (actor.IsLeadership())
        {
            return;
        }
        var agentId = actor.GetAgentId();
        var isEL = agentId is not null && await db.PersonGroupAgents
            .AnyAsync(a => a.PersonGroupId == groupId && a.AgentId == agentId && a.IsInvestigationLead, cancellationToken);
        if (!isEL)
        {
            throw new UnauthorizedAccessException(
                "Agents zuteilen oder entfernen dürfen nur die Führung oder ein Ermittlungsleiter dieser Akte.");
        }
    }

    public async Task<PersonGroupProgress> GetProgressAsync(string groupId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // join to People so the soft-delete filter counts only members with a live record
        var captured = await db.PersonGroupMembers
            .Where(m => m.PersonGroupId == groupId)
            .Join(db.People, m => m.PersonId, p => p.Id, (m, p) => m.Id)
            .CountAsync(cancellationToken);
        var estimated = await db.PersonGroups
            .Where(g => g.Id == groupId)
            .Select(g => g.EstimatedMemberCount)
            .FirstOrDefaultAsync(cancellationToken);
        return new PersonGroupProgress(captured, estimated);
    }

    public async Task<List<AuditLog>> GetHistoryAsync(string groupId, bool isLeadership, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await Visibility.IsRecordVisibleAsync(db, nameof(PersonGroup), groupId, isLeadership, cancellationToken))
        {
            return new();
        }
        var memberIds = await db.PersonGroupMembers
            .Where(m => m.PersonGroupId == groupId)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);
        var agentAllocationIds = await db.PersonGroupAgents
            .Where(a => a.PersonGroupId == groupId)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        // manual links touching this group, including removed ones so their delete entry shows
        var relationIds = await db.Links
            .IgnoreQueryFilters()
            .Where(v => !v.Automatic
                && ((v.SourceType == nameof(PersonGroup) && v.SourceId == groupId)
                 || (v.TargetType == nameof(PersonGroup) && v.TargetId == groupId)))
            .Select(v => v.Id)
            .ToListAsync(cancellationToken);

        var ids = new HashSet<string>(memberIds) { groupId };
        ids.UnionWith(agentAllocationIds);
        ids.UnionWith(relationIds);
        var types = new[] { nameof(PersonGroup), nameof(PersonGroupMember), nameof(PersonGroupAgent), nameof(Link) };

        return await db.AuditLogs
            .Where(a => types.Contains(a.EntityType) && ids.Contains(a.EntityId))
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Syncs the person's automatic group-colleague links: one exists iff two people share a group.</summary>
    private static async Task GroupColleaguesSyncAsync(AppDbContext db, string personId, CancellationToken cancellationToken)
    {
        var myGroups = await db.PersonGroupMembers
            .Where(m => m.PersonId == personId)
            .Select(m => m.PersonGroupId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var should = myGroups.Count == 0
            ? new List<string>()
            : await db.PersonGroupMembers
                .Where(m => myGroups.Contains(m.PersonGroupId) && m.PersonId != personId)
                .Select(m => m.PersonId)
                .Distinct()
                .ToListAsync(cancellationToken);

        await ColleaguesSync.SyncAsync(db, personId, ColleaguesSync.GroupColleague, should, cancellationToken);
    }
}
