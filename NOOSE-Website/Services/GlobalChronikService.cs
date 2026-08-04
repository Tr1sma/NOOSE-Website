using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Cases;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.Groups;
using NOOSE_Website.Data.Entities.Jobs;
using NOOSE_Website.Data.Entities.Operations;
using NOOSE_Website.Data.Entities.Parties;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Timeline;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IGlobalChronikService" />
public class GlobalChronikService(IDbContextFactory<AppDbContext> dbFactory) : IGlobalChronikService
{
    private const int MaxRows = 400;

    // record-level swimlanes (no child fan-out)
    private static readonly string[] SwimlaneTypes =
    {
        nameof(Person), nameof(Faction), nameof(PersonGroup), nameof(Party), nameof(Operation),
        nameof(Case), nameof(Taskforce), nameof(Job), nameof(Document), nameof(Law),
    };

    public async Task<ChronikResult> GetEventsAsync(ChronikQuery query, ClaimsPrincipal viewer, CancellationToken cancellationToken = default)
    {
        var scope = ViewerScope.From(viewer);
        if (scope.IsPartner)
        {
            return new ChronikResult(Array.Empty<ChronikEvent>(), false);
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var types = query.TypeFilter is { } tf && SwimlaneTypes.Contains(tf) ? new[] { tf } : SwimlaneTypes;

        // ---- audit lifecycle (record-level only) ----
        var auditQ = db.AuditLogs.Where(a => types.Contains(a.EntityType)
            && a.Timestamp >= query.FromUtc && a.Timestamp <= query.ToUtc);
        if (!string.IsNullOrEmpty(query.AgentId))
        {
            auditQ = auditQ.Where(a => a.AgentId == query.AgentId);
        }
        var auditRows = await auditQ.OrderByDescending(a => a.Timestamp).Take(MaxRows + 1)
            .Select(a => new { a.Timestamp, a.EntityType, a.EntityId, a.Action, a.AgentName })
            .ToListAsync(cancellationToken);

        // ---- classification history (Person/Faction/PersonGroup) ----
        var classTypes = types.Where(t => t is nameof(Person) or nameof(Faction) or nameof(PersonGroup)).ToArray();
        var classRows = classTypes.Length == 0
            ? new List<ClassRow>()
            : await BuildClassQuery(db, classTypes, query)
                .OrderByDescending(e => e.Timestamp).Take(MaxRows + 1)
                .Select(e => new ClassRow(e.Timestamp, e.EntityType, e.EntityId, e.Value, e.AgentName))
                .ToListAsync(cancellationToken);

        var capped = auditRows.Count > MaxRows || classRows.Count > MaxRows;

        var byType = new Dictionary<string, HashSet<string>>();
        void Note(string type, string id)
        {
            if (!byType.TryGetValue(type, out var set))
            {
                set = new HashSet<string>();
                byType[type] = set;
            }
            set.Add(id);
        }
        foreach (var a in auditRows) { Note(a.EntityType, a.EntityId); }
        foreach (var c in classRows) { Note(c.EntityType, c.EntityId); }

        var names = await ResolveVisibleAsync(db, scope, byType, cancellationToken);

        var events = new List<ChronikEvent>(auditRows.Count + classRows.Count);
        foreach (var a in auditRows)
        {
            if (!names.TryGetValue((a.EntityType, a.EntityId), out var name))
            {
                continue; // not visible / not resolvable
            }
            var (kat, verb) = TimelineDisplay.MapAudit(a.EntityType, a.Action);
            events.Add(new ChronikEvent(a.Timestamp, kat, a.EntityType, a.EntityId, name, verb,
                a.AgentName, Href(a.EntityType, a.EntityId)));
        }
        foreach (var c in classRows)
        {
            if (!names.TryGetValue((c.EntityType, c.EntityId), out var name))
            {
                continue;
            }
            events.Add(new ChronikEvent(c.Timestamp, TimelineCategory.Classification, c.EntityType, c.EntityId, name,
                $"Einstufung: {ClassificationDisplay.Name(c.Value)}", c.AgentName, Href(c.EntityType, c.EntityId)));
        }

        var ordered = events.OrderByDescending(e => e.Timestamp).ToList();
        if (ordered.Count > MaxRows)
        {
            ordered = ordered.Take(MaxRows).ToList();
            capped = true;
        }
        return new ChronikResult(ordered, capped);
    }

    public async Task<ChronikFilterOptions> GetFilterOptionsAsync(ClaimsPrincipal viewer, CancellationToken cancellationToken = default)
    {
        var scope = ViewerScope.From(viewer);
        if (scope.IsPartner)
        {
            return new ChronikFilterOptions(Array.Empty<string>(), Array.Empty<ChronikAgentOption>());
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var agents = await db.AuditLogs.Where(a => a.AgentId != null && a.AgentName != null)
            .Select(a => new { a.AgentId, a.AgentName })
            .Distinct().Take(300)
            .ToListAsync(cancellationToken);
        var agentOpts = agents
            .Where(a => a.AgentId is not null)
            .GroupBy(a => a.AgentId!)
            .Select(g => new ChronikAgentOption(g.Key, g.First().AgentName ?? g.Key))
            .OrderBy(a => a.Name)
            .ToList();
        return new ChronikFilterOptions(SwimlaneTypes.ToList(), agentOpts);
    }

    private sealed record ClassRow(DateTime Timestamp, string EntityType, string EntityId, Classification Value, string? AgentName);

    private static IQueryable<ClassificationHistory> BuildClassQuery(AppDbContext db, string[] classTypes, ChronikQuery query)
    {
        var q = db.ClassificationHistory.Where(e => classTypes.Contains(e.EntityType)
            && e.Timestamp >= query.FromUtc && e.Timestamp <= query.ToUtc);
        return string.IsNullOrEmpty(query.AgentId) ? q : q.Where(e => e.AgentId == query.AgentId);
    }

    // resolves display names ONLY for records the viewer may see; IgnoreQueryFilters so deletion/restore events survive
    private static async Task<Dictionary<(string Type, string Id), string>> ResolveVisibleAsync(
        AppDbContext db, ViewerScope scope, Dictionary<string, HashSet<string>> byType, CancellationToken ct)
    {
        var result = new Dictionary<(string, string), string>();
        List<string> Ids(string type) => byType.TryGetValue(type, out var s) ? s.ToList() : new List<string>();

        bool VisRestricted(bool c, bool tru, bool hrb) => scope.CanSee(
            !c ? DocumentClassification.None : tru ? DocumentClassification.Tru : hrb ? DocumentClassification.Hrb : DocumentClassification.Leadership);
        bool VisDocument(bool c, bool tru, bool hrb) => scope.CanSee(
            c ? DocumentClassification.Leadership : tru ? DocumentClassification.Tru : hrb ? DocumentClassification.Hrb : DocumentClassification.None);

        var personIds = Ids(nameof(Person));
        if (personIds.Count > 0)
        {
            foreach (var x in await db.People.IgnoreQueryFilters().Where(p => personIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name, p.IsClassified, p.IsTRUClassified, p.IsHRBClassified }).ToListAsync(ct))
            {
                if (VisRestricted(x.IsClassified, x.IsTRUClassified, x.IsHRBClassified)) { result[(nameof(Person), x.Id)] = x.Name; }
            }
        }

        var factionIds = Ids(nameof(Faction));
        if (factionIds.Count > 0)
        {
            foreach (var x in await db.Factions.IgnoreQueryFilters().Where(f => factionIds.Contains(f.Id))
                .Select(f => new { f.Id, f.Name, f.IsClassified, f.IsTRUClassified, f.IsHRBClassified }).ToListAsync(ct))
            {
                if (VisRestricted(x.IsClassified, x.IsTRUClassified, x.IsHRBClassified)) { result[(nameof(Faction), x.Id)] = x.Name; }
            }
        }

        var groupIds = Ids(nameof(PersonGroup));
        if (groupIds.Count > 0)
        {
            foreach (var x in await db.PersonGroups.IgnoreQueryFilters().Where(g => groupIds.Contains(g.Id))
                .Select(g => new { g.Id, g.Name, g.IsClassified, g.IsTRUClassified, g.IsHRBClassified }).ToListAsync(ct))
            {
                if (VisRestricted(x.IsClassified, x.IsTRUClassified, x.IsHRBClassified)) { result[(nameof(PersonGroup), x.Id)] = x.Name; }
            }
        }

        var partyIds = Ids(nameof(Party));
        if (partyIds.Count > 0)
        {
            foreach (var x in await db.Parties.IgnoreQueryFilters().Where(p => partyIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name, p.IsClassified, p.IsTRUClassified, p.IsHRBClassified }).ToListAsync(ct))
            {
                if (VisRestricted(x.IsClassified, x.IsTRUClassified, x.IsHRBClassified)) { result[(nameof(Party), x.Id)] = x.Name; }
            }
        }

        var operationIds = Ids(nameof(Operation));
        if (operationIds.Count > 0)
        {
            foreach (var x in await db.Operations.IgnoreQueryFilters().Where(o => operationIds.Contains(o.Id))
                .Select(o => new { o.Id, o.Title, o.IsClassified, o.IsTRUClassified, o.IsHRBClassified }).ToListAsync(ct))
            {
                if (VisRestricted(x.IsClassified, x.IsTRUClassified, x.IsHRBClassified)) { result[(nameof(Operation), x.Id)] = x.Title; }
            }
        }

        var caseIds = Ids(nameof(Case));
        if (caseIds.Count > 0)
        {
            foreach (var x in await db.Cases.IgnoreQueryFilters().Where(v => caseIds.Contains(v.Id))
                .Select(v => new { v.Id, v.Title, v.IsClassified, v.IsTRUClassified, v.IsHRBClassified }).ToListAsync(ct))
            {
                if (VisRestricted(x.IsClassified, x.IsTRUClassified, x.IsHRBClassified)) { result[(nameof(Case), x.Id)] = x.Title; }
            }
        }

        var documentIds = Ids(nameof(Document));
        if (documentIds.Count > 0)
        {
            foreach (var x in await db.Documents.IgnoreQueryFilters().Where(d => documentIds.Contains(d.Id))
                .Select(d => new { d.Id, d.Title, d.IsClassified, d.IsTRUClassified, d.IsHRBClassified }).ToListAsync(ct))
            {
                if (VisDocument(x.IsClassified, x.IsTRUClassified, x.IsHRBClassified))
                {
                    result[(nameof(Document), x.Id)] = string.IsNullOrWhiteSpace(x.Title) ? "Dokument" : x.Title;
                }
            }
        }

        var lawIds = Ids(nameof(Law));
        if (lawIds.Count > 0)
        {
            foreach (var x in await db.Laws.IgnoreQueryFilters().Where(g => lawIds.Contains(g.Id))
                .Select(g => new { g.Id, g.Paragraph, g.Title }).ToListAsync(ct))
            {
                result[(nameof(Law), x.Id)] = $"{x.Paragraph} {x.Title}".Trim();
            }
        }

        var tfIds = Ids(nameof(Taskforce));
        if (tfIds.Count > 0)
        {
            var visible = await TaskforceVisibility.VisibleIdsAsync(db, tfIds, scope.MayAllTaskforces, scope.MeId, ct);
            if (visible.Count > 0)
            {
                foreach (var x in await db.Taskforces.IgnoreQueryFilters().Where(t => visible.Contains(t.Id))
                    .Select(t => new { t.Id, t.Name }).ToListAsync(ct))
                {
                    result[(nameof(Taskforce), x.Id)] = x.Name;
                }
            }
        }

        var jobIds = Ids(nameof(Job));
        if (jobIds.Count > 0)
        {
            var visible = await JobVisibility.VisibleIdsAsync(db, jobIds, scope.MayAllTaskforces, scope.MeId, ct);
            if (visible.Count > 0)
            {
                foreach (var x in await db.Jobs.IgnoreQueryFilters().Where(a => visible.Contains(a.Id))
                    .Select(a => new { a.Id, a.Title }).ToListAsync(ct))
                {
                    result[(nameof(Job), x.Id)] = x.Title;
                }
            }
        }

        return result;
    }

    private static string? Href(string type, string id) => type switch
    {
        nameof(Person) => $"/personen/{id}",
        nameof(Faction) => $"/fraktionen/{id}",
        nameof(PersonGroup) => $"/personengruppen/{id}",
        nameof(Party) => $"/parteien/{id}",
        nameof(Operation) => $"/operationen/{id}",
        nameof(Case) => $"/vorgaenge/{id}",
        nameof(Taskforce) => $"/taskforces/{id}",
        nameof(Job) => $"/aufgaben/{id}",
        nameof(Document) => $"/dokumente/{id}",
        nameof(Law) => $"/gesetze/{id}",
        _ => null,
    };
}
