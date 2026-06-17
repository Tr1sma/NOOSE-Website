using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Infrastructure.Storage;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Factions;
using NOOSE_Website.Models.People;

namespace NOOSE_Website.Services;

public class FactionService(IDbContextFactory<AppDbContext> dbFactory, ICaseNumberService caseNumber, IProfileSuggestionService suggestion, IPersonService personService, IFactionPhotoStorageService photoStorage, IThreatScoreService threat) : IFactionService
{
    public async Task<List<Faction>> GetListAsync(ViewerScope scope, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Include members+person so the list member count matches the detail view.
        return await VisibleFactions(db, scope)
            .Include(f => f.Members).ThenInclude(m => m.Person)
            .Include(f => f.Photos)
            .OrderByDescending(f => f.ModifiedAt ?? f.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Faction?> GetDetailAsync(string id, ViewerScope scope, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var faction = await db.Factions
            .Include(f => f.Ranks)
            .Include(f => f.WeaponStock)
            .Include(f => f.Inventory)
            .Include(f => f.DrugRoutes)
            .Include(f => f.Photos)
            .AsSplitQuery()
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

        if (faction is null || !await Visibility.IsRecordVisibleAsync(db, nameof(Faction), id, scope, cancellationToken))
        {
            return null;
        }
        return faction;
    }

    public async Task<List<Faction>> GetTrashAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Factions.IgnoreQueryFilters()
            .Where(f => f.IsDeleted)
            .OrderByDescending(f => f.DeletedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Faction>> SearchAsync(string? searchText, bool isLeadership, int max = 20, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Factions.Where(f => isLeadership || !f.IsClassified);

        var s = searchText?.Trim();
        if (!string.IsNullOrEmpty(s))
        {
            query = query.Where(f => f.Name.Contains(s) || f.CaseNumber.Contains(s));
        }

        return await query
            .OrderBy(f => f.Name)
            .Take(max)
            .ToListAsync(cancellationToken);
    }

    public async Task<Faction> CreateAsync(FactionInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        ClassificationHelper.CheckRankGate(input.Classification, actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var faction = new Faction
        {
            CaseNumber = await caseNumber.NextAsync(db, "F", cancellationToken),
            Name = input.Name.Trim(),
            Kind = input.Kind.TrimToNull(),
            Radio = input.Radio.TrimToNull(),
            Darkchat = input.Darkchat.TrimToNull(),
            IssuingTimes = input.IssuingTimes.TrimToNull(),
            Estate = input.Estate.TrimToNull(),
            RecognitionColor = input.RecognitionColor.TrimToNull(),
            Targets = input.Targets.TrimToNull(),
            Description = input.Description.TrimToNull(),
            Classification = input.Classification,
            IsClassified = input.IsClassified,
            IsStateFaction = input.IsStateFaction,
            EstimatedMemberCount = input.EstimatedMemberCount,
        };
        ChildrenMap(faction, input);
        await SuggestionsStageAsync(db, faction, cancellationToken);

        if (input.Classification != Classification.Unknown)
        {
            db.ClassificationHistory.Add(ClassificationHelper.Entry(nameof(Faction), faction.Id, input.Classification, input.ClassificationJustification, actor));
        }

        db.Factions.Add(faction);
        await db.SaveChangesAsync(cancellationToken);

        // Take over the members from the create form (existing + auto-created, deduplicated), then build colleague links.
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
                db.FactionMembers.Add(new FactionMember
                {
                    FactionId = faction.Id,
                    PersonId = pid,
                    Rank = m.Rank.TrimToNull(),
                    IsLead = m.IsLead,
                });
                added.Add(pid);
            }
            if (added.Count > 0)
            {
                await db.SaveChangesAsync(cancellationToken);
                foreach (var pid in added)
                {
                    await FactionColleaguesSyncAsync(db, pid, cancellationToken);
                }
            }
        }

        // Auto-assign the creator as investigation lead so at least one always exists.
        var creatorId = actor.GetAgentId();
        if (creatorId is not null)
        {
            db.FactionAgents.Add(new FactionAgent
            {
                FactionId = faction.Id,
                AgentId = creatorId,
                IsInvestigationLead = true,
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
        // Initial threat score now that classification/members/stocks exist.
        await threat.NewCalculateAsync(faction.Id, cancellationToken);
        return faction;
    }

    public async Task RefreshAsync(string id, FactionInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var faction = await db.Factions
            .Include(f => f.Ranks)
            .Include(f => f.WeaponStock)
            .Include(f => f.Inventory)
            .Include(f => f.DrugRoutes)
            .AsSplitQuery()
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Fraktion '{id}' nicht gefunden.");

        if (faction.IsClassified && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        faction.Name = input.Name.Trim();
        faction.Kind = input.Kind.TrimToNull();
        faction.Radio = input.Radio.TrimToNull();
        faction.Darkchat = input.Darkchat.TrimToNull();
        faction.IssuingTimes = input.IssuingTimes.TrimToNull();
        faction.Estate = input.Estate.TrimToNull();
        faction.RecognitionColor = input.RecognitionColor.TrimToNull();
        faction.Targets = input.Targets.TrimToNull();
        faction.Description = input.Description.TrimToNull();
        faction.IsClassified = input.IsClassified;
        faction.IsStateFaction = input.IsStateFaction;
        faction.EstimatedMemberCount = input.EstimatedMemberCount;

        // Detect renames before replacing the old ranks, so the denormalized rank name on members can follow.
        var renames = RankRenamesDetect(faction.Ranks, input.Ranks);

        // Replace the structured lists wholesale; members are untouched (own endpoints).
        db.FactionRanks.RemoveRange(faction.Ranks);
        db.FactionWeaponStocks.RemoveRange(faction.WeaponStock);
        db.FactionInventories.RemoveRange(faction.Inventory);
        db.FactionDrugRoutes.RemoveRange(faction.DrugRoutes);
        ChildrenMap(faction, input);
        await SuggestionsStageAsync(db, faction, cancellationToken);

        // Carry renamed ranks over to the member list (rank is a denormalized copy there).
        if (renames.Count > 0)
        {
            var members = await db.FactionMembers
                .Where(m => m.FactionId == id && m.Rank != null)
                .ToListAsync(cancellationToken);
            foreach (var m in members)
            {
                if (m.Rank is not null && renames.TryGetValue(m.Rank, out var newName))
                {
                    m.Rank = newName;
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        // Master data affects the score.
        await threat.NewCalculateAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var faction = await db.Factions.FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Fraktion '{id}' nicht gefunden.");
        // Interceptor rewrites Remove to soft-delete.
        db.Factions.Remove(faction);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var faction = await db.Factions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Fraktion '{id}' nicht gefunden.");

        faction.IsDeleted = false;
        faction.DeletedAt = null;
        faction.DeletedById = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ClassificationSetAsync(string id, Classification @new, string? justification, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        ClassificationHelper.CheckRankGate(@new, actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var faction = await db.Factions.FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Fraktion '{id}' nicht gefunden.");

        if (faction.IsClassified && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        faction.Classification = @new;
        db.ClassificationHistory.Add(ClassificationHelper.Entry(nameof(Faction), id, @new, justification, actor));
        await db.SaveChangesAsync(cancellationToken);
        // Classification sets the score's minimum band.
        await threat.NewCalculateAsync(id, cancellationToken);
    }

    public async Task<List<ClassificationHistory>> GetClassificationHistoryAsync(string id, ViewerScope scope, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await Visibility.IsRecordVisibleAsync(db, nameof(Faction), id, scope, cancellationToken))
        {
            return new();
        }
        return await db.ClassificationHistory
            .Where(e => e.EntityType == nameof(Faction) && e.EntityId == id)
            .OrderByDescending(e => e.Timestamp)
            .ToListAsync(cancellationToken);
    }

    private static IQueryable<Faction> VisibleFactions(AppDbContext db, ViewerScope scope)
        => scope.PartnerAgency is { } agency
            ? db.Factions.OnlyPartnerVisible(db, agency, scope.MeId)
            : db.Factions.Where(f => scope.MayClassifiedRead || !f.IsClassified);

    public async Task<List<FactionMember>> GetMembersAsync(string factionId, ViewerScope scope, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var members = await db.FactionMembers
            .Where(m => m.FactionId == factionId)
            .Include(m => m.Person)
            .ToListAsync(cancellationToken);

        // Person == null means the record is trashed; classified people are leadership-only.
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

    public async Task MemberAddAsync(string factionId, MemberInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var faction = await db.Factions.FirstOrDefaultAsync(f => f.Id == factionId, cancellationToken)
            ?? throw new InvalidOperationException($"Fraktion '{factionId}' nicht gefunden.");
        if (faction.IsClassified && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        var personId = await PersonIdDetermineAsync(db, input.PersonId, input.NewPersonName, actor, cancellationToken);
        if (await db.FactionMembers.AnyAsync(m => m.FactionId == factionId && m.PersonId == personId, cancellationToken))
        {
            throw new InvalidOperationException("Diese Person ist bereits Mitglied der Fraktion.");
        }

        // Membership and colleague links in one transaction, so no intermediate state leaks.
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        db.FactionMembers.Add(new FactionMember
        {
            FactionId = factionId,
            PersonId = personId,
            Rank = input.Rank.TrimToNull(),
            IsLead = input.IsLead,
        });
        await db.SaveChangesAsync(cancellationToken);
        await FactionColleaguesSyncAsync(db, personId, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        // Member count/lead affect the faction score; the new member brings its measure heat.
        await threat.NewCalculateAsync(factionId, cancellationToken);
        // Membership/lead role affect the person score.
        await threat.NewCalculatePersonScoreAsync(personId, cancellationToken);
    }

    /// <summary>Returns the person id: the existing one (checked) or a freshly created person record.</summary>
    private Task<string> PersonIdDetermineAsync(AppDbContext db, string? personId, string? newName, ClaimsPrincipal actor, CancellationToken cancellationToken)
        => MemberHelper.PersonIdDetermineAsync(db, personService, personId, newName, actor, cancellationToken);

    public async Task MemberChangeAsync(string memberId, string? rank, bool isLead, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var member = await db.FactionMembers.Include(m => m.Faction).FirstOrDefaultAsync(m => m.Id == memberId, cancellationToken)
            ?? throw new InvalidOperationException("Mitgliedschaft nicht gefunden.");
        if (member.Faction?.IsClassified == true && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }
        member.Rank = rank.TrimToNull();
        member.IsLead = isLead;
        await db.SaveChangesAsync(cancellationToken);
        // Lead flag affects both the faction and the person score.
        await threat.NewCalculateAsync(member.FactionId, cancellationToken);
        await threat.NewCalculatePersonScoreAsync(member.PersonId, cancellationToken);
    }

    public async Task MemberRemoveAsync(string memberId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var member = await db.FactionMembers.Include(m => m.Faction).FirstOrDefaultAsync(m => m.Id == memberId, cancellationToken);
        if (member is null)
        {
            return;
        }
        if (member.Faction?.IsClassified == true && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }
        var personId = member.PersonId;
        var factionId = member.FactionId;
        // Departure and colleague-link update in one transaction.
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        // Soft-delete keeps the membership as a history entry; colleague sync then sees only active members.
        db.FactionMembers.Remove(member);
        await db.SaveChangesAsync(cancellationToken);
        await FactionColleaguesSyncAsync(db, personId, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        // Departure affects faction size/lead; the member's heat stays stable.
        await threat.NewCalculateAsync(factionId, cancellationToken);
        // Departure changes the person's memberships/lead roles.
        await threat.NewCalculatePersonScoreAsync(personId, cancellationToken);
    }

    public async Task<List<FactionAgent>> GetAgentsAsync(string factionId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.FactionAgents
            .Where(a => a.FactionId == factionId)
            .Include(a => a.Agent)
            .OrderByDescending(a => a.IsInvestigationLead)
            .ThenBy(a => a.Agent!.Codename)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<FactionAgent>> GetInvestigationLeadAsync(string factionId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.FactionAgents
            .Where(a => a.FactionId == factionId && a.IsInvestigationLead)
            .Include(a => a.Agent)
            .OrderBy(a => a.Agent!.Codename)
            .ToListAsync(cancellationToken);
    }

    public async Task AgentAllocateAsync(string factionId, string agentId, bool asInvestigationLead, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var faction = await db.Factions.FirstOrDefaultAsync(f => f.Id == factionId, cancellationToken)
            ?? throw new InvalidOperationException($"Fraktion '{factionId}' nicht gefunden.");
        if (faction.IsClassified && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }
        await RequireLeadershipOrELAsync(db, factionId, actor, cancellationToken);
        // Only leadership may grant the investigation-lead flag.
        if (asInvestigationLead)
        {
            Permission.RequireLeadership(actor);
        }
        if (!await db.Users.AnyAsync(u => u.Id == agentId, cancellationToken))
        {
            throw new InvalidOperationException("Der gewählte Agent wurde nicht gefunden.");
        }
        if (await db.FactionAgents.AnyAsync(a => a.FactionId == factionId && a.AgentId == agentId, cancellationToken))
        {
            throw new InvalidOperationException("Dieser Agent ist der Fraktion bereits zugeteilt.");
        }

        db.FactionAgents.Add(new FactionAgent
        {
            FactionId = factionId,
            AgentId = agentId,
            IsInvestigationLead = asInvestigationLead,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AgentRemoveAsync(string allocationId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var allocation = await db.FactionAgents.Include(a => a.Faction).FirstOrDefaultAsync(a => a.Id == allocationId, cancellationToken);
        if (allocation is null)
        {
            return;
        }
        if (allocation.Faction?.IsClassified == true && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }
        await RequireLeadershipOrELAsync(db, allocation.FactionId, actor, cancellationToken);
        db.FactionAgents.Remove(allocation);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task InvestigationLeadSetAsync(string allocationId, bool @is, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        // Granting/revoking the investigation lead is leadership-only.
        Permission.RequireLeadership(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var allocation = await db.FactionAgents.FirstOrDefaultAsync(a => a.Id == allocationId, cancellationToken)
            ?? throw new InvalidOperationException("Zuteilung nicht gefunden.");
        allocation.IsInvestigationLead = @is;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Throws unless the actor is leadership or an investigation lead of this faction.</summary>
    private static async Task RequireLeadershipOrELAsync(AppDbContext db, string factionId, ClaimsPrincipal actor, CancellationToken cancellationToken)
    {
        if (actor.IsLeadership())
        {
            return;
        }
        var agentId = actor.GetAgentId();
        var isEL = agentId is not null && await db.FactionAgents
            .AnyAsync(a => a.FactionId == factionId && a.AgentId == agentId && a.IsInvestigationLead, cancellationToken);
        if (!isEL)
        {
            throw new UnauthorizedAccessException(
                "Agents zuteilen oder entfernen dürfen nur die Führung oder ein Ermittlungsleiter dieser Akte.");
        }
    }

    public async Task<List<AuditLog>> GetHistoryAsync(string factionId, bool isLeadership, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await Visibility.IsRecordVisibleAsync(db, nameof(Faction), factionId, isLeadership, cancellationToken))
        {
            return new();
        }
        var memberIds = await db.FactionMembers
            .Where(m => m.FactionId == factionId)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);
        var agentAllocationIds = await db.FactionAgents
            .Where(a => a.FactionId == factionId)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        // Manual relations touching this faction, including removed ones so their "removed" entry still shows.
        var relationIds = await db.Links
            .IgnoreQueryFilters()
            .Where(v => !v.Automatic
                && ((v.SourceType == nameof(Faction) && v.SourceId == factionId)
                 || (v.TargetType == nameof(Faction) && v.TargetId == factionId)))
            .Select(v => v.Id)
            .ToListAsync(cancellationToken);

        var ids = new HashSet<string>(memberIds) { factionId };
        ids.UnionWith(agentAllocationIds);
        ids.UnionWith(relationIds);
        var types = new[] { nameof(Faction), nameof(FactionMember), nameof(FactionAgent), nameof(Link) };

        return await db.AuditLogs
            .Where(a => types.Contains(a.EntityType) && ids.Contains(a.EntityId))
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<FactionActivity>> GetActivitiesAsync(string factionId, ViewerScope scope, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Defense in depth: classified-faction activities are leadership-only.
        if (!await Visibility.IsRecordVisibleAsync(db, nameof(Faction), factionId, scope, cancellationToken))
        {
            return new();
        }
        var activities = await db.FactionActivities
            .Where(a => a.FactionId == factionId)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync(cancellationToken);
        if (scope.PartnerAgency is { } agency)
        {
            activities = await PartnerVisibility.FilterChildrenAsync(db, nameof(Faction), factionId, nameof(FactionActivity), activities, a => a.Id, agency, scope.MeId, cancellationToken);
        }
        return activities;
    }

    public async Task ActivityAddAsync(string factionId, ActivityInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        var title = input.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new InvalidOperationException("Ein Titel ist erforderlich.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var faction = await db.Factions.FirstOrDefaultAsync(f => f.Id == factionId, cancellationToken)
            ?? throw new InvalidOperationException($"Fraktion '{factionId}' nicht gefunden.");
        if (faction.IsClassified && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        db.FactionActivities.Add(new FactionActivity
        {
            FactionId = factionId,
            Title = title,
            Kind = input.Kind.TrimToNull(),
            // Store the user-picked local time as UTC (app convention).
            Timestamp = input.Timestamp.ToUniversalTime(),
            Description = input.Description.TrimToNull(),
            Location = input.Location.TrimToNull(),
        });
        await db.SaveChangesAsync(cancellationToken);
        // An activity is a dated incident, core of the S1 heat.
        await threat.NewCalculateAsync(factionId, cancellationToken);
    }

    public async Task ActivityChangeAsync(string activityId, ActivityInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        var title = input.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new InvalidOperationException("Ein Titel ist erforderlich.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var activity = await db.FactionActivities.Include(a => a.Faction).FirstOrDefaultAsync(a => a.Id == activityId, cancellationToken)
            ?? throw new InvalidOperationException("Aktivität nicht gefunden.");
        if (activity.Faction?.IsClassified == true && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        activity.Title = title;
        activity.Kind = input.Kind.TrimToNull();
        activity.Timestamp = input.Timestamp.ToUniversalTime();
        activity.Description = input.Description.TrimToNull();
        activity.Location = input.Location.TrimToNull();
        await db.SaveChangesAsync(cancellationToken);
        // Kind/time of an activity affect S1.
        await threat.NewCalculateAsync(activity.FactionId, cancellationToken);
    }

    public async Task ActivityRemoveAsync(string activityId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var activity = await db.FactionActivities.Include(a => a.Faction).FirstOrDefaultAsync(a => a.Id == activityId, cancellationToken);
        if (activity is null)
        {
            return;
        }
        if (activity.Faction?.IsClassified == true && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }
        var factionId = activity.FactionId;
        db.FactionActivities.Remove(activity);
        await db.SaveChangesAsync(cancellationToken);
        // Removed incident drops out of S1.
        await threat.NewCalculateAsync(factionId, cancellationToken);
    }

    public async Task<List<string>> GetActivityKindsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Distinct over all activities so common kinds appear as suggestions everywhere.
        return await db.FactionActivities
            .Where(a => a.Kind != null && a.Kind != "")
            .Select(a => a.Kind!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<FactionPhoto>> GetPhotosAsync(string factionId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Title image first, then by capture time.
        return await db.FactionPhotos
            .Where(f => f.FactionId == factionId)
            .OrderByDescending(f => f.IsTitleImage)
            .ThenBy(f => f.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<FactionPhoto?> GetPhotoWithFactionAsync(string photoId, ViewerScope scope, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var photo = await db.FactionPhotos.Include(f => f.Faction).FirstOrDefaultAsync(f => f.Id == photoId, cancellationToken);
        if (photo?.Faction is null)
        {
            return null;
        }
        if (scope.PartnerAgency is { } agency)
        {
            // partners: parent visible AND (whole-record or this photo released)
            return await PartnerVisibility.IsChildVisibleToPartnerAsync(db, nameof(Faction), photo.FactionId, nameof(FactionPhoto), photoId, agency, scope.MeId, cancellationToken)
                ? photo
                : null;
        }
        return photo.Faction.IsClassified && !scope.MayClassifiedRead ? null : photo;
    }

    public async Task<FactionPhoto> PhotoAddAsync(string factionId, Stream content, string originalName, string contentType, long size, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        if (!photoStorage.IsAllowedType(contentType))
        {
            throw new InvalidOperationException($"Dateityp '{contentType}' ist nicht erlaubt.");
        }
        // Enforce the size limit server-side, not just in the UI.
        if (size > photoStorage.MaxBytes)
        {
            throw new InvalidOperationException($"Datei zu groß (max. {photoStorage.MaxBytes / (1024 * 1024)} MB).");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Check existence and visibility before writing a file.
        var faction = await db.Factions.FirstOrDefaultAsync(f => f.Id == factionId, cancellationToken)
            ?? throw new InvalidOperationException($"Fraktion '{factionId}' nicht gefunden.");
        if (faction.IsClassified && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        // The first photo becomes the title image automatically.
        var isFirst = !await db.FactionPhotos.AnyAsync(f => f.FactionId == factionId, cancellationToken);

        var fileName = await photoStorage.SaveAsync(content, contentType, cancellationToken);
        var photo = new FactionPhoto
        {
            FactionId = factionId,
            FileNameSaved = fileName,
            OriginalName = originalName,
            ContentType = contentType,
            SizeBytes = size,
            IsTitleImage = isFirst,
            CreatedAt = DateTime.UtcNow,
            CreatedById = actor.GetAgentId(),
        };
        db.FactionPhotos.Add(photo);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Remove the written file if the DB insert fails, to avoid an orphaned attachment.
            photoStorage.Delete(fileName);
            throw;
        }
        return photo;
    }

    public async Task PhotoRemoveAsync(string photoId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var photo = await db.FactionPhotos.Include(f => f.Faction).FirstOrDefaultAsync(f => f.Id == photoId, cancellationToken);
        if (photo is null)
        {
            return;
        }
        if (photo.Faction?.IsClassified == true && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }
        // Remove the DB record first, then the file, so a storage error leaves no record pointing at a missing file.
        db.FactionPhotos.Remove(photo);
        await db.SaveChangesAsync(cancellationToken);
        photoStorage.Delete(photo.FileNameSaved);
    }

    public async Task AsTitleImageSetAsync(string photoId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var photo = await db.FactionPhotos.Include(f => f.Faction).FirstOrDefaultAsync(f => f.Id == photoId, cancellationToken)
            ?? throw new InvalidOperationException($"Foto '{photoId}' nicht gefunden.");
        if (photo.Faction?.IsClassified == true && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        // Exactly one title image per faction: clear all siblings, mark this one.
        var siblings = await db.FactionPhotos.Where(f => f.FactionId == photo.FactionId).ToListAsync(cancellationToken);
        foreach (var g in siblings)
        {
            g.IsTitleImage = g.Id == photoId;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Detects rank renames (old to new designation) by stable rank id; only actual changes count.</summary>
    private static Dictionary<string, string> RankRenamesDetect(IEnumerable<FactionRank> existing, IEnumerable<RankInput> input)
    {
        var oldById = existing.ToDictionary(r => r.Id, r => r.Designation);
        var renames = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var ri in input)
        {
            if (string.IsNullOrWhiteSpace(ri.Id) || !oldById.TryGetValue(ri.Id, out var oldName))
            {
                continue;
            }
            var newName = ri.Designation?.Trim();
            if (!string.IsNullOrWhiteSpace(newName) && !string.Equals(oldName, newName, StringComparison.Ordinal))
            {
                renames[oldName] = newName;
            }
        }
        return renames;
    }

    private static void ChildrenMap(Faction faction, FactionInput input)
    {
        faction.Ranks = input.Ranks
            .Where(r => !string.IsNullOrWhiteSpace(r.Designation))
            .Select((r, i) => new FactionRank { FactionId = faction.Id, Designation = r.Designation.Trim(), Order = i })
            .ToList();
        faction.WeaponStock = input.WeaponStock
            .Where(w => !string.IsNullOrWhiteSpace(w.Designation))
            .Select(w => new FactionWeaponStock { FactionId = faction.Id, Designation = w.Designation.Trim(), Quantity = w.Quantity.TrimToNull() })
            .ToList();
        faction.Inventory = input.Inventory
            .Where(l => !string.IsNullOrWhiteSpace(l.Designation))
            .Select(l => new FactionInventory { FactionId = faction.Id, Designation = l.Designation.Trim(), Quantity = l.Quantity.TrimToNull() })
            .ToList();
        // Drug routes share the generic stock input; its quantity field carries the note here.
        faction.DrugRoutes = input.DrugRoutes
            .Where(d => !string.IsNullOrWhiteSpace(d.Designation))
            .Select(d => new FactionDrugRoute { FactionId = faction.Id, Designation = d.Designation.Trim(), Note = d.Quantity.TrimToNull() })
            .ToList();
    }

    /// <summary>Stages stock designations into the shared suggestion catalog; classified factions are excluded, staged within the caller's SaveChanges.</summary>
    private async Task SuggestionsStageAsync(AppDbContext db, Faction faction, CancellationToken cancellationToken)
    {
        if (faction.IsClassified)
        {
            return;
        }
        if (!string.IsNullOrWhiteSpace(faction.Kind))
        {
            await suggestion.StageAsync(db, SuggestionType.Kind, new[] { faction.Kind }, cancellationToken);
        }
        await suggestion.StageAsync(db, SuggestionType.Weapon, faction.WeaponStock.Select(w => w.Designation), cancellationToken);
        await suggestion.StageAsync(db, SuggestionType.Inventory, faction.Inventory.Select(l => l.Designation), cancellationToken);
        await suggestion.StageAsync(db, SuggestionType.DrugRoute, faction.DrugRoutes.Select(d => d.Designation), cancellationToken);
    }

    /// <summary>Syncs the person's automatic faction-colleague links: a link exists iff two people share at least one faction. Called after every membership change.</summary>
    private static async Task FactionColleaguesSyncAsync(AppDbContext db, string personId, CancellationToken cancellationToken)
    {
        var myFactions = await db.FactionMembers
            .Where(m => m.PersonId == personId)
            .Select(m => m.FactionId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var should = myFactions.Count == 0
            ? new List<string>()
            : await db.FactionMembers
                .Where(m => myFactions.Contains(m.FactionId) && m.PersonId != personId)
                .Select(m => m.PersonId)
                .Distinct()
                .ToListAsync(cancellationToken);

        await ColleaguesSync.SyncAsync(db, personId, ColleaguesSync.FactionColleague, should, cancellationToken);
    }
}
