using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Parties;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Parties;
using NOOSE_Website.Models.People;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IParteiService" />
public class PartyService(IDbContextFactory<AppDbContext> dbFactory, ICaseNumberService caseNumber, IProfileSuggestionService suggestion, IPersonService personService, IThreatScoreService threat) : IPartyService
{
    public async Task<List<Party>> GetListAsync(ViewerScope scope, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Mitglieder inkl. Person laden, damit die Listen-Mitgliederzahl exakt der Detailansicht entspricht.
        return await VisibleParties(db, scope)
            .Include(p => p.Members).ThenInclude(m => m.Person)
            .OrderByDescending(p => p.ModifiedAt ?? p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Party?> GetDetailAsync(string id, ViewerScope scope, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var party = await db.Parties.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (party is null || !await Visibility.IsRecordVisibleAsync(db, nameof(Party), id, scope, cancellationToken))
        {
            return null;
        }
        return party;
    }

    public async Task<List<Party>> GetTrashAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Parties.IgnoreQueryFilters()
            .Where(p => p.IsDeleted)
            .OrderByDescending(p => p.DeletedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Party>> SearchAsync(string? searchText, bool isLeadership, int max = 20, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Parties.Where(p => isLeadership || !p.IsClassified);

        var s = searchText?.Trim();
        if (!string.IsNullOrEmpty(s))
        {
            query = query.Where(p => p.Name.Contains(s) || p.CaseNumber.Contains(s));
        }

        return await query
            .OrderBy(p => p.Name)
            .Take(max)
            .ToListAsync(cancellationToken);
    }

    public async Task<Party> CreateAsync(PartyInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        ClassificationHelper.CheckRankGate(input.Classification, actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var party = new Party
        {
            CaseNumber = await caseNumber.NextAsync(db, "PT", cancellationToken),
            Name = input.Name.Trim(),
            Description = Empty(input.Description),
            Targets = Empty(input.Targets),
            Remarks = Empty(input.Remarks),
            Classification = input.Classification,
            IsClassified = input.IsClassified,
        };

        if (input.Classification != Classification.Unknown)
        {
            db.ClassificationHistory.Add(ClassificationHelper.Entry(nameof(Party), party.Id, input.Classification, input.ClassificationJustification, actor));
        }

        db.Parties.Add(party);
        await db.SaveChangesAsync(cancellationToken);

        // Im Anlege-Formular erfasste Mitglieder übernehmen (bestehende Personen + automatisch angelegte
        // neue Akten, dedupliziert) und anschließend die Parteikollegen-Verknüpfungen aufbauen.
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
                    // Derselbe neue Name im selben Formular → nur EINE Akte anlegen (keine Dubletten).
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
                db.PartyMembers.Add(new PartyMember
                {
                    PartyId = party.Id,
                    PersonId = pid,
                    Role = Empty(m.Role),
                    IsLead = m.IsLead,
                });
                added.Add(pid);
            }
            if (added.Count > 0)
            {
                await SuggestionsStageAsync(db, party.IsClassified, input.Members.Select(m => m.Role), cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
                foreach (var pid in added)
                {
                    await PartyColleaguesSyncAsync(db, pid, cancellationToken);
                }
            }
        }

        // Ersteller automatisch zuteilen und als Ermittlungsleiter markieren (so existiert stets mindestens ein EL).
        var creatorId = actor.GetAgentId();
        if (creatorId is not null)
        {
            db.PartyAgents.Add(new PartyAgent
            {
                PartyId = party.Id,
                AgentId = creatorId,
                IsInvestigationLead = true,
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
        return party;
    }

    public async Task RefreshAsync(string id, PartyInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var party = await db.Parties.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Partei '{id}' nicht gefunden.");

        if (party.IsClassified && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        party.Name = input.Name.Trim();
        party.Description = Empty(input.Description);
        party.Targets = Empty(input.Targets);
        party.Remarks = Empty(input.Remarks);
        party.IsClassified = input.IsClassified;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var party = await db.Parties.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Partei '{id}' nicht gefunden.");
        db.Parties.Remove(party);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireLeadership(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var party = await db.Parties.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Partei '{id}' nicht gefunden.");

        party.IsDeleted = false;
        party.DeletedAt = null;
        party.DeletedById = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ClassificationSetAsync(string id, Classification @new, string? justification, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        ClassificationHelper.CheckRankGate(@new, actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var party = await db.Parties.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Partei '{id}' nicht gefunden.");

        if (party.IsClassified && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        party.Classification = @new;
        db.ClassificationHistory.Add(ClassificationHelper.Entry(nameof(Party), id, @new, justification, actor));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<ClassificationHistory>> GetClassificationHistoryAsync(string id, ViewerScope scope, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await Visibility.IsRecordVisibleAsync(db, nameof(Party), id, scope, cancellationToken))
        {
            return new();
        }
        return await db.ClassificationHistory
            .Where(e => e.EntityType == nameof(Party) && e.EntityId == id)
            .OrderByDescending(e => e.Timestamp)
            .ToListAsync(cancellationToken);
    }

    // scope-filtered party query
    private static IQueryable<Party> VisibleParties(AppDbContext db, ViewerScope scope)
        => scope.PartnerAgency is { } agency
            ? db.Parties.OnlyPartnerVisible(db, agency, scope.MeId)
            : db.Parties.Where(p => scope.MayClassifiedRead || !p.IsClassified);

    public async Task<List<PartyMember>> GetMembersAsync(string partyId, ViewerScope scope, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var members = await db.PartyMembers
            .Where(m => m.PartyId == partyId)
            .Include(m => m.Person)
            .ToListAsync(cancellationToken);

        // Person == null → trashed; hide. Classified persons only for leadership.
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

    public async Task MemberAddAsync(string partyId, PartyMemberInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var party = await db.Parties.FirstOrDefaultAsync(p => p.Id == partyId, cancellationToken)
            ?? throw new InvalidOperationException($"Partei '{partyId}' nicht gefunden.");
        if (party.IsClassified && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }

        var personId = await PersonIdDetermineAsync(db, input.PersonId, input.NewPersonName, actor, cancellationToken);
        if (await db.PartyMembers.AnyAsync(m => m.PartyId == partyId && m.PersonId == personId, cancellationToken))
        {
            throw new InvalidOperationException("Diese Person ist bereits Mitglied der Partei.");
        }

        // Mitgliedschaft + automatische Parteikollegen-Verknüpfungen in EINER Transaktion.
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        db.PartyMembers.Add(new PartyMember
        {
            PartyId = partyId,
            PersonId = personId,
            Role = Empty(input.Role),
            IsLead = input.IsLead,
        });
        await SuggestionsStageAsync(db, party.IsClassified, new[] { input.Role }, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await PartyColleaguesSyncAsync(db, personId, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        await threat.NewCalculatePersonScoreAsync(personId, cancellationToken);
    }

    /// <summary>
    /// Liefert die Personen-Id: bestehende (mit Existenzprüfung) oder – bei nur neuem Namen – eine frisch
    /// angelegte Personen-Akte (committet, eigenes Aktenzeichen).
    /// </summary>
    private Task<string> PersonIdDetermineAsync(AppDbContext db, string? personId, string? newName, ClaimsPrincipal actor, CancellationToken cancellationToken)
        => MemberHelper.PersonIdDetermineAsync(db, personService, personId, newName, actor, cancellationToken);

    public async Task MemberChangeAsync(string memberId, string? role, bool isLead, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var member = await db.PartyMembers.Include(m => m.Party).FirstOrDefaultAsync(m => m.Id == memberId, cancellationToken)
            ?? throw new InvalidOperationException("Mitgliedschaft nicht gefunden.");
        if (member.Party?.IsClassified == true && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }
        member.Role = Empty(role);
        member.IsLead = isLead;
        await SuggestionsStageAsync(db, member.Party?.IsClassified == true, new[] { role }, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await threat.NewCalculatePersonScoreAsync(member.PersonId, cancellationToken);
    }

    public async Task MemberRemoveAsync(string memberId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var member = await db.PartyMembers.Include(m => m.Party).FirstOrDefaultAsync(m => m.Id == memberId, cancellationToken);
        if (member is null)
        {
            return;
        }
        if (member.Party?.IsClassified == true && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }
        var personId = member.PersonId;
        // Austritt + Kollegen-Verknüpfungen in EINER Transaktion. Soft-Delete (ISoftDelete): der Interceptor setzt
        // GeloeschtAm (= Austrittsdatum) statt hart zu löschen → Mitgliedschaft bleibt als Verlaufseintrag erhalten.
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        db.PartyMembers.Remove(member);
        await db.SaveChangesAsync(cancellationToken);
        await PartyColleaguesSyncAsync(db, personId, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        await threat.NewCalculatePersonScoreAsync(personId, cancellationToken);
    }

    public async Task<List<PartyAgent>> GetAgentsAsync(string partyId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.PartyAgents
            .Where(a => a.PartyId == partyId)
            .Include(a => a.Agent)
            .OrderByDescending(a => a.IsInvestigationLead)
            .ThenBy(a => a.Agent!.Codename)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<PartyAgent>> GetInvestigationLeadAsync(string partyId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.PartyAgents
            .Where(a => a.PartyId == partyId && a.IsInvestigationLead)
            .Include(a => a.Agent)
            .OrderBy(a => a.Agent!.Codename)
            .ToListAsync(cancellationToken);
    }

    public async Task AgentAllocateAsync(string partyId, string agentId, bool asInvestigationLead, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var party = await db.Parties.FirstOrDefaultAsync(p => p.Id == partyId, cancellationToken)
            ?? throw new InvalidOperationException($"Partei '{partyId}' nicht gefunden.");
        if (party.IsClassified && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }
        await RequireLeadershipOrELAsync(db, partyId, actor, cancellationToken);
        // Das Ermittlungsleiter-Flag darf nur die Führung vergeben (auch beim Zuteilen).
        if (asInvestigationLead)
        {
            Permission.RequireLeadership(actor);
        }
        if (!await db.Users.AnyAsync(u => u.Id == agentId, cancellationToken))
        {
            throw new InvalidOperationException("Der gewählte Agent wurde nicht gefunden.");
        }
        if (await db.PartyAgents.AnyAsync(a => a.PartyId == partyId && a.AgentId == agentId, cancellationToken))
        {
            throw new InvalidOperationException("Dieser Agent ist der Partei bereits zugeteilt.");
        }

        db.PartyAgents.Add(new PartyAgent
        {
            PartyId = partyId,
            AgentId = agentId,
            IsInvestigationLead = asInvestigationLead,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AgentRemoveAsync(string allocationId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var allocation = await db.PartyAgents.Include(a => a.Party).FirstOrDefaultAsync(a => a.Id == allocationId, cancellationToken);
        if (allocation is null)
        {
            return;
        }
        if (allocation.Party?.IsClassified == true && !actor.IsLeadership())
        {
            throw new UnauthorizedAccessException("Diese Akte ist als Verschlusssache nur für die Führung zugänglich.");
        }
        await RequireLeadershipOrELAsync(db, allocation.PartyId, actor, cancellationToken);
        db.PartyAgents.Remove(allocation);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task InvestigationLeadSetAsync(string allocationId, bool @is, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        // Ermittlungsleiter vergeben/entziehen ist der Führung vorbehalten.
        Permission.RequireLeadership(actor);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var allocation = await db.PartyAgents.FirstOrDefaultAsync(a => a.Id == allocationId, cancellationToken)
            ?? throw new InvalidOperationException("Zuteilung nicht gefunden.");
        allocation.IsInvestigationLead = @is;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Wirft, wenn der Handelnde weder Führung noch Ermittlungsleiter dieser Partei ist.</summary>
    private static async Task RequireLeadershipOrELAsync(AppDbContext db, string partyId, ClaimsPrincipal actor, CancellationToken cancellationToken)
    {
        if (actor.IsLeadership())
        {
            return;
        }
        var agentId = actor.GetAgentId();
        var isEL = agentId is not null && await db.PartyAgents
            .AnyAsync(a => a.PartyId == partyId && a.AgentId == agentId && a.IsInvestigationLead, cancellationToken);
        if (!isEL)
        {
            throw new UnauthorizedAccessException(
                "Agents zuteilen oder entfernen dürfen nur die Führung oder ein Ermittlungsleiter dieser Akte.");
        }
    }

    public async Task<List<AuditLog>> GetHistoryAsync(string partyId, bool isLeadership, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await Visibility.IsRecordVisibleAsync(db, nameof(Party), partyId, isLeadership, cancellationToken))
        {
            return new();
        }
        var memberIds = await db.PartyMembers
            .Where(m => m.PartyId == partyId)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);
        var agentAllocationIds = await db.PartyAgents
            .Where(a => a.PartyId == partyId)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        // Manuelle Beziehungen (Konflikte/Bündnisse), die diese Partei als Quelle oder Ziel berühren –
        // inkl. bereits entfernter (IgnoreQueryFilters), damit auch deren „entfernt"-Eintrag erscheint.
        var relationIds = await db.Links
            .IgnoreQueryFilters()
            .Where(v => !v.Automatic
                && ((v.SourceType == nameof(Party) && v.SourceId == partyId)
                 || (v.TargetType == nameof(Party) && v.TargetId == partyId)))
            .Select(v => v.Id)
            .ToListAsync(cancellationToken);

        var ids = new HashSet<string>(memberIds) { partyId };
        ids.UnionWith(agentAllocationIds);
        ids.UnionWith(relationIds);
        var types = new[] { nameof(Party), nameof(PartyMember), nameof(PartyAgent), nameof(Link) };

        return await db.AuditLogs
            .Where(a => types.Contains(a.EntityType) && ids.Contains(a.EntityId))
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Synchronisiert die automatischen „Parteikollege"-Verknüpfungen der Person (analog zu den
    /// Fraktions-/Gruppenkollegen): zwischen P und Q soll genau dann eine bestehen, wenn beide mindestens
    /// eine Partei teilen. Wird nach jeder Mitglieder-Änderung für die betroffene Person aufgerufen.
    /// </summary>
    private static async Task PartyColleaguesSyncAsync(AppDbContext db, string personId, CancellationToken cancellationToken)
    {
        var myParties = await db.PartyMembers
            .Where(m => m.PersonId == personId)
            .Select(m => m.PartyId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var should = myParties.Count == 0
            ? new List<string>()
            : await db.PartyMembers
                .Where(m => myParties.Contains(m.PartyId) && m.PersonId != personId)
                .Select(m => m.PersonId)
                .Distinct()
                .ToListAsync(cancellationToken);

        await ColleaguesSync.SyncAsync(db, personId, ColleaguesSync.PartyColleague, should, cancellationToken);
    }

    /// <summary>
    /// Merkt eingegebene Mitglieds-Rollen im gemeinsamen Vorschlagskatalog vor (Autocomplete beim nächsten
    /// Mal), analog zu den Steckbrief-Feldern und der Fraktions-Art. Verschlusssachen bleiben außen vor,
    /// damit keine sensiblen Werte in die geteilte Liste gelangen. Nur vormerken – der Aufrufer speichert
    /// im selben SaveChanges (atomar mit der Mitgliedschaft).
    /// </summary>
    private async Task SuggestionsStageAsync(AppDbContext db, bool isClassified, IEnumerable<string?> roles, CancellationToken cancellationToken)
    {
        if (isClassified)
        {
            return;
        }
        await suggestion.StageAsync(db, SuggestionType.PartyRole, roles.Where(r => r is not null).Select(r => r!), cancellationToken);
    }

    private static string? Empty(string? s) => s.TrimToNull();
}
