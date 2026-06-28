using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.Groups;
using NOOSE_Website.Data.Entities.Operations;
using NOOSE_Website.Data.Entities.Parties;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Data.Entities.Cases;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Dashboard;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

public class DashboardService(IDbContextFactory<AppDbContext> dbFactory, IRequestService requestService,
    IRecencyService recency) : IDashboardService
{
    public async Task<DashboardMetrics> GetMetricsAsync(bool isLeadership, string? meId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Classification filter mirrors each list view so the tile matches its hit count.
        var people = await db.People.CountAsync(p => isLeadership || !p.IsClassified, cancellationToken);
        var factions = await db.Factions.CountAsync(f => isLeadership || !f.IsClassified, cancellationToken);
        var groups = await db.PersonGroups.CountAsync(g => isLeadership || !g.IsClassified, cancellationToken);
        var parties = await db.Parties.CountAsync(p => isLeadership || !p.IsClassified, cancellationToken);
        var operations = await db.Operations.CountAsync(o => isLeadership || !o.IsClassified, cancellationToken);

        // Open cases = not yet completed/archived.
        var openCases = await db.Cases.CountAsync(v => (isLeadership || !v.IsClassified)
            && v.Status != CaseStatus.Completed && v.Status != CaseStatus.Archived, cancellationToken);

        // Open requests = upgrades + pending registrations + name changes + requested taskforces + promotions.
        var openRequests = await requestService.GetOpenCountAsync(isLeadership, cancellationToken)
            + await db.Users.CountAsync(a => a.Status == AgentStatus.Pending, cancellationToken)
            + await db.Users.CountAsync(a => a.NameChangeRequestedAt != null, cancellationToken)
            + await db.Taskforces.OnlyVisible(db, isLeadership, meId).CountAsync(t => t.Status == TaskforceStatus.Requested, cancellationToken)
            + await db.AgentPromotionRequests.CountAsync(a => a.Status == PromotionStatus.Requested, cancellationToken);

        // The classified count is itself classified, so leadership-only.
        var classified = 0;
        if (isLeadership)
        {
            classified =
                  await db.People.CountAsync(p => p.IsClassified, cancellationToken)
                + await db.Factions.CountAsync(f => f.IsClassified, cancellationToken)
                + await db.PersonGroups.CountAsync(g => g.IsClassified, cancellationToken)
                + await db.Parties.CountAsync(p => p.IsClassified, cancellationToken)
                + await db.Operations.CountAsync(o => o.IsClassified, cancellationToken)
                + await db.Taskforces.CountAsync(t => t.IsClassified, cancellationToken)
                + await db.Cases.CountAsync(v => v.IsClassified, cancellationToken);
        }

        // Stale records: per type past the configured red threshold, referenced by ModifiedAt ?? CreatedAt.
        var settings = await recency.GetAllSettingsAsync(cancellationToken);
        var now = DateTime.UtcNow;
        DateTime CutoffDate(string type) => now.AddDays(-settings[type].StaleDays);
        var sP = CutoffDate(nameof(Person));
        var sF = CutoffDate(nameof(Faction));
        var sG = CutoffDate(nameof(PersonGroup));
        var sPt = CutoffDate(nameof(Party));
        var sO = CutoffDate(nameof(Operation));
        var sT = CutoffDate(nameof(Taskforce));
        var sV = CutoffDate(nameof(Case));
        // A type with aging disabled contributes nothing; exempt records drop out per type.
        var staleRecords =
              (settings[nameof(Person)].AgingDisabled ? 0 : await db.People.CountAsync(p => (isLeadership || !p.IsClassified) && !p.AgingDisabled && (p.ModifiedAt ?? p.CreatedAt) < sP, cancellationToken))
            + (settings[nameof(Faction)].AgingDisabled ? 0 : await db.Factions.CountAsync(f => (isLeadership || !f.IsClassified) && !f.IsStateFaction && !f.AgingDisabled && (f.ModifiedAt ?? f.CreatedAt) < sF, cancellationToken))
            + (settings[nameof(PersonGroup)].AgingDisabled ? 0 : await db.PersonGroups.CountAsync(g => (isLeadership || !g.IsClassified) && !g.AgingDisabled && (g.ModifiedAt ?? g.CreatedAt) < sG, cancellationToken))
            + (settings[nameof(Party)].AgingDisabled ? 0 : await db.Parties.CountAsync(p => (isLeadership || !p.IsClassified) && !p.AgingDisabled && (p.ModifiedAt ?? p.CreatedAt) < sPt, cancellationToken))
            + (settings[nameof(Operation)].AgingDisabled ? 0 : await db.Operations.CountAsync(o => (isLeadership || !o.IsClassified) && !o.AgingDisabled && (o.ModifiedAt ?? o.CreatedAt) < sO, cancellationToken))
            + (settings[nameof(Taskforce)].AgingDisabled ? 0 : await db.Taskforces.OnlyVisible(db, isLeadership, meId).CountAsync(t => !t.AgingDisabled && (t.ModifiedAt ?? t.CreatedAt) < sT, cancellationToken))
            + (settings[nameof(Case)].AgingDisabled ? 0 : await db.Cases.CountAsync(v => (isLeadership || !v.IsClassified) && !v.AgingDisabled && (v.ModifiedAt ?? v.CreatedAt) < sV, cancellationToken));

        // The org tile bundles factions, groups and parties; operations are their own tile.
        return new DashboardMetrics(people, factions + groups + parties, operations, openCases, openRequests, classified, staleRecords);
    }

    public async Task<List<DashboardStaleRecord>> GetUpdateNeedAsync(bool isLeadership, string? meId, int max = 30,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var settings = await recency.GetAllSettingsAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var result = new List<DashboardStaleRecord>();

        // Update need starts at the yellow threshold; load the oldest N per type, then globally sort and cap.
        // A type with aging disabled is skipped entirely; exempt records drop out per type.
        var setP = settings[nameof(Person)];
        if (!setP.AgingDisabled)
        {
            var cutP = now.AddDays(-setP.WarningDays);
            foreach (var x in await db.People
                .Where(p => (isLeadership || !p.IsClassified) && !p.AgingDisabled && (p.ModifiedAt ?? p.CreatedAt) < cutP)
                .OrderBy(p => p.ModifiedAt ?? p.CreatedAt)
                .Select(p => new { p.Id, p.Name, p.CaseNumber, Reference = p.ModifiedAt ?? p.CreatedAt })
                .Take(max).ToListAsync(cancellationToken))
            {
                result.Add(new DashboardStaleRecord(DashboardRecordType.Person, x.Name, x.CaseNumber, $"/personen/{x.Id}",
                    RecencyAssessment.Level(setP.WarningDays, setP.StaleDays, x.Reference, now), x.Reference));
            }
        }

        var setF = settings[nameof(Faction)];
        if (!setF.AgingDisabled)
        {
            var cutF = now.AddDays(-setF.WarningDays);
            foreach (var x in await db.Factions
                .Where(f => (isLeadership || !f.IsClassified) && !f.IsStateFaction && !f.AgingDisabled && (f.ModifiedAt ?? f.CreatedAt) < cutF)
                .OrderBy(f => f.ModifiedAt ?? f.CreatedAt)
                .Select(f => new { f.Id, f.Name, f.CaseNumber, Reference = f.ModifiedAt ?? f.CreatedAt })
                .Take(max).ToListAsync(cancellationToken))
            {
                result.Add(new DashboardStaleRecord(DashboardRecordType.Faction, x.Name, x.CaseNumber, $"/fraktionen/{x.Id}",
                    RecencyAssessment.Level(setF.WarningDays, setF.StaleDays, x.Reference, now), x.Reference));
            }
        }

        var setG = settings[nameof(PersonGroup)];
        if (!setG.AgingDisabled)
        {
            var cutG = now.AddDays(-setG.WarningDays);
            foreach (var x in await db.PersonGroups
                .Where(g => (isLeadership || !g.IsClassified) && !g.AgingDisabled && (g.ModifiedAt ?? g.CreatedAt) < cutG)
                .OrderBy(g => g.ModifiedAt ?? g.CreatedAt)
                .Select(g => new { g.Id, g.Name, g.CaseNumber, Reference = g.ModifiedAt ?? g.CreatedAt })
                .Take(max).ToListAsync(cancellationToken))
            {
                result.Add(new DashboardStaleRecord(DashboardRecordType.PersonGroup, x.Name, x.CaseNumber, $"/personengruppen/{x.Id}",
                    RecencyAssessment.Level(setG.WarningDays, setG.StaleDays, x.Reference, now), x.Reference));
            }
        }

        var setPt = settings[nameof(Party)];
        if (!setPt.AgingDisabled)
        {
            var cutPt = now.AddDays(-setPt.WarningDays);
            foreach (var x in await db.Parties
                .Where(p => (isLeadership || !p.IsClassified) && !p.AgingDisabled && (p.ModifiedAt ?? p.CreatedAt) < cutPt)
                .OrderBy(p => p.ModifiedAt ?? p.CreatedAt)
                .Select(p => new { p.Id, p.Name, p.CaseNumber, Reference = p.ModifiedAt ?? p.CreatedAt })
                .Take(max).ToListAsync(cancellationToken))
            {
                result.Add(new DashboardStaleRecord(DashboardRecordType.Party, x.Name, x.CaseNumber, $"/parteien/{x.Id}",
                    RecencyAssessment.Level(setPt.WarningDays, setPt.StaleDays, x.Reference, now), x.Reference));
            }
        }

        var setO = settings[nameof(Operation)];
        if (!setO.AgingDisabled)
        {
            var cutO = now.AddDays(-setO.WarningDays);
            foreach (var x in await db.Operations
                .Where(o => (isLeadership || !o.IsClassified) && !o.AgingDisabled && (o.ModifiedAt ?? o.CreatedAt) < cutO)
                .OrderBy(o => o.ModifiedAt ?? o.CreatedAt)
                .Select(o => new { o.Id, Name = o.Title, o.CaseNumber, Reference = o.ModifiedAt ?? o.CreatedAt })
                .Take(max).ToListAsync(cancellationToken))
            {
                result.Add(new DashboardStaleRecord(DashboardRecordType.Operation, x.Name, x.CaseNumber, $"/operationen/{x.Id}",
                    RecencyAssessment.Level(setO.WarningDays, setO.StaleDays, x.Reference, now), x.Reference));
            }
        }

        var setT = settings[nameof(Taskforce)];
        if (!setT.AgingDisabled)
        {
            var cutT = now.AddDays(-setT.WarningDays);
            foreach (var x in await db.Taskforces.OnlyVisible(db, isLeadership, meId)
                .Where(t => !t.AgingDisabled && (t.ModifiedAt ?? t.CreatedAt) < cutT)
                .OrderBy(t => t.ModifiedAt ?? t.CreatedAt)
                .Select(t => new { t.Id, t.Name, t.CaseNumber, Reference = t.ModifiedAt ?? t.CreatedAt })
                .Take(max).ToListAsync(cancellationToken))
            {
                result.Add(new DashboardStaleRecord(DashboardRecordType.Taskforce, x.Name, x.CaseNumber, $"/taskforces/{x.Id}",
                    RecencyAssessment.Level(setT.WarningDays, setT.StaleDays, x.Reference, now), x.Reference));
            }
        }

        var setV = settings[nameof(Case)];
        if (!setV.AgingDisabled)
        {
            var cutV = now.AddDays(-setV.WarningDays);
            foreach (var x in await db.Cases
                .Where(v => (isLeadership || !v.IsClassified) && !v.AgingDisabled && (v.ModifiedAt ?? v.CreatedAt) < cutV)
                .OrderBy(v => v.ModifiedAt ?? v.CreatedAt)
                .Select(v => new { v.Id, Name = v.Title, v.CaseNumber, Reference = v.ModifiedAt ?? v.CreatedAt })
                .Take(max).ToListAsync(cancellationToken))
            {
                result.Add(new DashboardStaleRecord(DashboardRecordType.Case, x.Name, x.CaseNumber, $"/vorgaenge/{x.Id}",
                    RecencyAssessment.Level(setV.WarningDays, setV.StaleDays, x.Reference, now), x.Reference));
            }
        }

        // Oldest first, then globally cap.
        return result.OrderBy(e => e.ReferenceUtc).Take(max).ToList();
    }

    public async Task<List<DashboardFactionHazard>> GetFactionsByHazardAsync(bool isLeadership,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Most dangerous first; hazard level derived on-read from the threat score, no score sorts last.
        var rows = await db.Factions
            .Where(f => isLeadership || !f.IsClassified)
            .OrderByDescending(f => f.ThreatScore ?? 0)
            .ThenBy(f => f.Name)
            .Select(f => new { f.Id, f.Name, f.CaseNumber, f.ThreatScore })
            .ToListAsync(cancellationToken);

        return rows.Select(f => new DashboardFactionHazard(
            f.Name, f.CaseNumber, $"/fraktionen/{f.Id}", HazardLevelLogic.From(f.ThreatScore))).ToList();
    }

    public async Task<List<DashboardFactionHazard>> GetPeopleByHazardAsync(bool isLeadership,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Counterpart to the faction tile, most dangerous first; only scored people (> 0), top 15.
        var rows = await db.People
            .Where(p => (isLeadership || !p.IsClassified) && p.ThreatScore != null && p.ThreatScore > 0)
            .OrderByDescending(p => p.ThreatScore)
            .ThenBy(p => p.Name)
            .Select(p => new { p.Id, p.Name, p.CaseNumber, p.ThreatScore })
            .Take(15)
            .ToListAsync(cancellationToken);

        return rows.Select(p => new DashboardFactionHazard(
            p.Name, p.CaseNumber, $"/personen/{p.Id}", HazardLevelLogic.From(p.ThreatScore))).ToList();
    }

    public async Task<DashboardDistributions> GetDistributionsAsync(bool isLeadership, string? meId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // All four distributions are classification-filtered like the metric tiles.

        // Cases by classification; all enum values filled so the legend stays stable.
        var classificationCount = (await db.Cases
                .Where(v => isLeadership || !v.IsClassified)
                .GroupBy(v => v.Classification)
                .Select(g => new { Value = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => x.Value, x => x.Count);
        var casesByClassification = ClassificationDisplay.All
            .Select(e => new DistributionSegment(ClassificationDisplay.Name(e), classificationCount.GetValueOrDefault(e)))
            .ToList();

        // Person-doc outcomes; classification filtered via the parent person (INNER JOIN also hides deleted ones).
        var outcomeCount = (await db.PersonDocs
                .Where(d => isLeadership || !d.Person!.IsClassified)
                .GroupBy(d => d.Outcome)
                .Select(g => new { Value = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken))
            .ToDictionary(x => x.Value, x => x.Count);
        var measureOutcomes = MeasureOutcomeDisplay.All
            .Select(a => new DistributionSegment(MeasureOutcomeDisplay.Name(a), outcomeCount.GetValueOrDefault(a)))
            .ToList();

        // Factions by hazard, derived on-read from the threat score; bucketed in-memory to avoid a CASE translation.
        var scores = await db.Factions
            .Where(f => isLeadership || !f.IsClassified)
            .Select(f => f.ThreatScore)
            .ToListAsync(cancellationToken);
        var hazardCount = scores
            .GroupBy(HazardLevelLogic.From)
            .ToDictionary(g => g.Key, g => g.Count());
        var factionsByHazard = HazardLevelLogic.All
            .Select(s => new DistributionSegment(HazardLevelLogic.Name(s), hazardCount.GetValueOrDefault(s)))
            .ToList();

        // Open requests by kind; same five sub-counts as the metric tile, broken out individually.
        var openRequestsByKind = new List<DistributionSegment>
        {
            new("Hochstufung", await requestService.GetOpenCountAsync(isLeadership, cancellationToken)),
            new("Registrierung", await db.Users.CountAsync(a => a.Status == AgentStatus.Pending, cancellationToken)),
            new("Namensänderung", await db.Users.CountAsync(a => a.NameChangeRequestedAt != null, cancellationToken)),
            new("Taskforce", await db.Taskforces.OnlyVisible(db, isLeadership, meId).CountAsync(t => t.Status == TaskforceStatus.Requested, cancellationToken)),
            new("Beförderung", await db.AgentPromotionRequests.CountAsync(a => a.Status == PromotionStatus.Requested, cancellationToken)),
        };

        return new DashboardDistributions(casesByClassification, measureOutcomes, factionsByHazard, openRequestsByKind);
    }

    public async Task<List<DashboardChange>> GetLastChangesAsync(bool isLeadership, string? meId, int max = 8, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Over-fetch: classification filter and unresolvable entries thin the list before we cap to max.
        var raw = await db.AuditLogs
            .OrderByDescending(a => a.Timestamp)
            .ThenByDescending(a => a.Id)
            .Take(Math.Max(max, 1) * 8)
            .ToListAsync(cancellationToken);

        if (raw.Count == 0)
        {
            return new List<DashboardChange>();
        }

        // Roll child entities up to their parent record via batch lookups.
        var docIds = Ids(raw, nameof(PersonDoc));
        var docToPerson = docIds.Count == 0 ? new Dictionary<string, string>()
            : await db.PersonDocs.IgnoreQueryFilters().Where(d => docIds.Contains(d.Id))
                .Select(d => new { d.Id, d.PersonId }).ToDictionaryAsync(x => x.Id, x => x.PersonId, cancellationToken);

        // IgnoreQueryFilters: a departure soft-deletes the membership, so the "member removed" event would otherwise be unmappable.
        var fmIds = Ids(raw, nameof(FactionMember));
        var memberToFaction = fmIds.Count == 0 ? new Dictionary<string, string>()
            : await db.FactionMembers.IgnoreQueryFilters().Where(m => fmIds.Contains(m.Id))
                .Select(m => new { m.Id, m.FactionId }).ToDictionaryAsync(x => x.Id, x => x.FactionId, cancellationToken);

        var pmIds = Ids(raw, nameof(PersonGroupMember));
        var memberToGroup = pmIds.Count == 0 ? new Dictionary<string, string>()
            : await db.PersonGroupMembers.IgnoreQueryFilters().Where(m => pmIds.Contains(m.Id))
                .Select(m => new { m.Id, m.PersonGroupId }).ToDictionaryAsync(x => x.Id, x => x.PersonGroupId, cancellationToken);

        var paIds = Ids(raw, nameof(PersonGroupAgent));
        var agentToGroup = paIds.Count == 0 ? new Dictionary<string, string>()
            : await db.PersonGroupAgents.Where(a => paIds.Contains(a.Id))
                .Select(a => new { a.Id, a.PersonGroupId }).ToDictionaryAsync(x => x.Id, x => x.PersonGroupId, cancellationToken);

        var pmPartyIds = Ids(raw, nameof(PartyMember));
        var memberToParty = pmPartyIds.Count == 0 ? new Dictionary<string, string>()
            : await db.PartyMembers.IgnoreQueryFilters().Where(m => pmPartyIds.Contains(m.Id))
                .Select(m => new { m.Id, m.PartyId }).ToDictionaryAsync(x => x.Id, x => x.PartyId, cancellationToken);

        var paPartyIds = Ids(raw, nameof(PartyAgent));
        var agentToParty = paPartyIds.Count == 0 ? new Dictionary<string, string>()
            : await db.PartyAgents.Where(a => paPartyIds.Contains(a.Id))
                .Select(a => new { a.Id, a.PartyId }).ToDictionaryAsync(x => x.Id, x => x.PartyId, cancellationToken);

        var oaIds = Ids(raw, nameof(OperationAgent));
        var agentToOperation = oaIds.Count == 0 ? new Dictionary<string, string>()
            : await db.OperationAgents.Where(a => oaIds.Contains(a.Id))
                .Select(a => new { a.Id, a.OperationId }).ToDictionaryAsync(x => x.Id, x => x.OperationId, cancellationToken);

        var taIds = Ids(raw, nameof(TaskforceAgent));
        var agentToTaskforce = taIds.Count == 0 ? new Dictionary<string, string>()
            : await db.TaskforceAgents.Where(a => taIds.Contains(a.Id))
                .Select(a => new { a.Id, a.TaskforceId }).ToDictionaryAsync(x => x.Id, x => x.TaskforceId, cancellationToken);

        var vaIds = Ids(raw, nameof(CaseAgent));
        var agentToCase = vaIds.Count == 0 ? new Dictionary<string, string>()
            : await db.CaseAgents.Where(a => vaIds.Contains(a.Id))
                .Select(a => new { a.Id, a.CaseId }).ToDictionaryAsync(x => x.Id, x => x.CaseId, cancellationToken);

        // Map each audit entry to a target record, or drop it.
        var targets = new List<(AuditLog Log, DashboardRecordType Type, string RecordId, string? Detail)>();
        foreach (var log in raw)
        {
            (DashboardRecordType Type, string RecordId, string? Detail)? target = log.EntityType switch
            {
                nameof(Person) => (DashboardRecordType.Person, log.EntityId, (string?)null),
                nameof(Faction) => (DashboardRecordType.Faction, log.EntityId, null),
                nameof(PersonGroup) => (DashboardRecordType.PersonGroup, log.EntityId, null),
                nameof(PersonDoc) when docToPerson.TryGetValue(log.EntityId, out var pid)
                    => (DashboardRecordType.Person, pid, "Dok"),
                nameof(FactionMember) when memberToFaction.TryGetValue(log.EntityId, out var fid)
                    => (DashboardRecordType.Faction, fid, "Mitglied"),
                nameof(PersonGroupMember) when memberToGroup.TryGetValue(log.EntityId, out var gid)
                    => (DashboardRecordType.PersonGroup, gid, "Mitglied"),
                nameof(PersonGroupAgent) when agentToGroup.TryGetValue(log.EntityId, out var gid2)
                    => (DashboardRecordType.PersonGroup, gid2, "Agent-Zuteilung"),
                nameof(Party) => (DashboardRecordType.Party, log.EntityId, null),
                nameof(PartyMember) when memberToParty.TryGetValue(log.EntityId, out var prid)
                    => (DashboardRecordType.Party, prid, "Mitglied"),
                nameof(PartyAgent) when agentToParty.TryGetValue(log.EntityId, out var prid2)
                    => (DashboardRecordType.Party, prid2, "Agent-Zuteilung"),
                nameof(Operation) => (DashboardRecordType.Operation, log.EntityId, null),
                nameof(OperationAgent) when agentToOperation.TryGetValue(log.EntityId, out var oid)
                    => (DashboardRecordType.Operation, oid, "Agent-Zuteilung"),
                nameof(Taskforce) => (DashboardRecordType.Taskforce, log.EntityId, null),
                nameof(TaskforceAgent) when agentToTaskforce.TryGetValue(log.EntityId, out var tid)
                    => (DashboardRecordType.Taskforce, tid, "Agent-Zuteilung"),
                nameof(Case) => (DashboardRecordType.Case, log.EntityId, null),
                nameof(CaseAgent) when agentToCase.TryGetValue(log.EntityId, out var vid)
                    => (DashboardRecordType.Case, vid, "Agent-Zuteilung"),
                _ => null,
            };

            if (target is { } z)
            {
                targets.Add((log, z.Type, z.RecordId, z.Detail));
            }
        }

        // Load display data in one batch, including trash so deleted records stay nameable.
        var personMap = await PersonInfos(db, TargetIds(targets, DashboardRecordType.Person), cancellationToken);
        var factionMap = await FactionInfos(db, TargetIds(targets, DashboardRecordType.Faction), cancellationToken);
        var groupMap = await GroupInfos(db, TargetIds(targets, DashboardRecordType.PersonGroup), cancellationToken);
        var partyMap = await PartyInfos(db, TargetIds(targets, DashboardRecordType.Party), cancellationToken);
        var operationMap = await OperationInfos(db, TargetIds(targets, DashboardRecordType.Operation), cancellationToken);
        var taskforceMap = await TaskforceInfos(db, TargetIds(targets, DashboardRecordType.Taskforce), cancellationToken);
        // For taskforces, membership (not classification) decides feed visibility.
        var visibleTf = await TaskforceVisibility.VisibleIdsAsync(db, TargetIds(targets, DashboardRecordType.Taskforce), isLeadership, meId, cancellationToken);
        var caseMap = await CaseInfos(db, TargetIds(targets, DashboardRecordType.Case), cancellationToken);

        var result = new List<DashboardChange>();
        foreach (var (log, type, recordId, detail) in targets)
        {
            var info = type switch
            {
                DashboardRecordType.Person => personMap.GetValueOrDefault(recordId),
                DashboardRecordType.Faction => factionMap.GetValueOrDefault(recordId),
                DashboardRecordType.Party => partyMap.GetValueOrDefault(recordId),
                DashboardRecordType.Operation => operationMap.GetValueOrDefault(recordId),
                DashboardRecordType.Taskforce => taskforceMap.GetValueOrDefault(recordId),
                DashboardRecordType.Case => caseMap.GetValueOrDefault(recordId),
                _ => groupMap.GetValueOrDefault(recordId),
            };

            // Record no longer resolvable.
            if (info is null)
            {
                continue;
            }
            // Taskforce gated by membership; other types by classification.
            if (type == DashboardRecordType.Taskforce)
            {
                if (!visibleTf.Contains(recordId))
                {
                    continue;
                }
            }
            else if (info.IsClassified && !isLeadership)
            {
                continue;
            }

            result.Add(new DashboardChange(
                log.Timestamp, log.AgentName, log.Action, type,
                recordId, info.Name, info.CaseNumber, detail, info.IsDeleted));

            if (result.Count >= max)
            {
                break;
            }
        }

        return result;
    }

    private sealed record RecordInfo(string Name, string CaseNumber, bool IsClassified, bool IsDeleted);

    private static List<string> Ids(List<AuditLog> logs, string type)
        => logs.Where(a => a.EntityType == type).Select(a => a.EntityId).Distinct().ToList();

    private static List<string> TargetIds(
        IEnumerable<(AuditLog Log, DashboardRecordType Type, string RecordId, string? Detail)> targets, DashboardRecordType type)
        => targets.Where(z => z.Type == type).Select(z => z.RecordId).Distinct().ToList();

    private static async Task<Dictionary<string, RecordInfo>> PersonInfos(AppDbContext db, List<string> ids, CancellationToken ct)
        => ids.Count == 0 ? new()
            : await db.People.IgnoreQueryFilters().Where(p => ids.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => new RecordInfo(p.Name, p.CaseNumber, p.IsClassified, p.IsDeleted), ct);

    private static async Task<Dictionary<string, RecordInfo>> FactionInfos(AppDbContext db, List<string> ids, CancellationToken ct)
        => ids.Count == 0 ? new()
            : await db.Factions.IgnoreQueryFilters().Where(f => ids.Contains(f.Id))
                .ToDictionaryAsync(f => f.Id, f => new RecordInfo(f.Name, f.CaseNumber, f.IsClassified, f.IsDeleted), ct);

    private static async Task<Dictionary<string, RecordInfo>> GroupInfos(AppDbContext db, List<string> ids, CancellationToken ct)
        => ids.Count == 0 ? new()
            : await db.PersonGroups.IgnoreQueryFilters().Where(g => ids.Contains(g.Id))
                .ToDictionaryAsync(g => g.Id, g => new RecordInfo(g.Name, g.CaseNumber, g.IsClassified, g.IsDeleted), ct);

    private static async Task<Dictionary<string, RecordInfo>> PartyInfos(AppDbContext db, List<string> ids, CancellationToken ct)
        => ids.Count == 0 ? new()
            : await db.Parties.IgnoreQueryFilters().Where(p => ids.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => new RecordInfo(p.Name, p.CaseNumber, p.IsClassified, p.IsDeleted), ct);

    private static async Task<Dictionary<string, RecordInfo>> OperationInfos(AppDbContext db, List<string> ids, CancellationToken ct)
        => ids.Count == 0 ? new()
            : await db.Operations.IgnoreQueryFilters().Where(o => ids.Contains(o.Id))
                .ToDictionaryAsync(o => o.Id, o => new RecordInfo(o.Title, o.CaseNumber, o.IsClassified, o.IsDeleted), ct);

    private static async Task<Dictionary<string, RecordInfo>> TaskforceInfos(AppDbContext db, List<string> ids, CancellationToken ct)
        => ids.Count == 0 ? new()
            : await db.Taskforces.IgnoreQueryFilters().Where(t => ids.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => new RecordInfo(t.Name, t.CaseNumber, t.IsClassified, t.IsDeleted), ct);

    private static async Task<Dictionary<string, RecordInfo>> CaseInfos(AppDbContext db, List<string> ids, CancellationToken ct)
        => ids.Count == 0 ? new()
            : await db.Cases.IgnoreQueryFilters().Where(v => ids.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id, v => new RecordInfo(v.Title, v.CaseNumber, v.IsClassified, v.IsDeleted), ct);
}
