using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Cases;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.Groups;
using NOOSE_Website.Data.Entities.Jobs;
using NOOSE_Website.Data.Entities.Operations;
using NOOSE_Website.Data.Entities.Parties;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Timeline;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IGlobalChronikService" />
public class GlobalChronikService(IDbContextFactory<AppDbContext> dbFactory) : IGlobalChronikService
{
    // one day never renders more than this; a busier day shows its newest events
    private const int SliceCap = 500;
    // day probes per page; bounds the work when filters reject nearly everything
    private const int MaxRounds = 8;
    // absolute score delta worth a chronicle line; the leadership alarm has its own configurable threshold
    private const int ScoreJumpThreshold = 10;
    // shown when a referenced record is classified or of a type the chronicle does not render
    private const string HiddenRecord = "nicht sichtbare Akte";

    /// <summary>Record types the chronicle anchors its events on.</summary>
    public static readonly string[] RecordTypes =
    {
        nameof(Person), nameof(Faction), nameof(PersonGroup), nameof(Party), nameof(Operation),
        nameof(Case), nameof(Taskforce), nameof(Job), nameof(Document), nameof(Law),
    };

    public async Task<ChronikResult> GetEventsAsync(ChronikQuery query, ClaimsPrincipal viewer, CancellationToken cancellationToken = default)
    {
        var scope = ViewerScope.From(viewer);
        if (scope.IsPartner)
        {
            return new ChronikResult(Array.Empty<ChronikEvent>(), null, false);
        }

        var filter = SliceFilter.From(query);
        if (filter.RecordTypes.Length == 0)
        {
            return new ChronikResult(Array.Empty<ChronikEvent>(), null, false);
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var events = new List<ChronikEvent>();
        var cursor = query.BeforeUtc ?? query.ToUtc;

        // walk whole local days backwards, skipping empty stretches via a cheap newest-timestamp probe
        for (var round = 0; round < MaxRounds && cursor > query.FromUtc && events.Count < query.MinEvents; round++)
        {
            var newest = await NewestBeforeAsync(db, query, filter, cursor, cancellationToken);
            if (newest is null)
            {
                cursor = query.FromUtc;
                break;
            }

            var dayStart = LocalDayStartUtc(newest.Value);
            var from = dayStart < query.FromUtc ? query.FromUtc : dayStart;
            events.AddRange(await LoadSliceAsync(db, scope, query, filter, from, cursor, cancellationToken));
            cursor = from;
        }

        var hasMore = cursor > query.FromUtc;
        return new ChronikResult(
            events.OrderByDescending(e => e.Timestamp).ToList(),
            hasMore ? cursor : null,
            hasMore);
    }

    public async Task<IReadOnlyList<ChronikDensityBucket>> GetDensityAsync(ChronikQuery query, ClaimsPrincipal viewer, CancellationToken cancellationToken = default)
    {
        var scope = ViewerScope.From(viewer);
        if (scope.IsPartner)
        {
            return Array.Empty<ChronikDensityBucket>();
        }

        var filter = SliceFilter.From(query);
        if (filter.RecordTypes.Length == 0)
        {
            return Array.Empty<ChronikDensityBucket>();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // grouped in SQL and in UTC; the band is an overview, the feed does the exact local-day grouping
        var days = await AuditSlice(db, filter, query.FromUtc, query.ToUtc)
            .GroupBy(a => new { a.Timestamp.Year, a.Timestamp.Month, a.Timestamp.Day })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var buckets = days
            .Select(d => (Start: new DateTime(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Utc), d.Count))
            .OrderBy(b => b.Start);

        if ((query.ToUtc - query.FromUtc).TotalDays <= 90)
        {
            return buckets.Select(b => new ChronikDensityBucket(b.Start, b.Count)).ToList();
        }

        return buckets
            .GroupBy(b => b.Start.AddDays(-(int)b.Start.DayOfWeek))
            .Select(g => new ChronikDensityBucket(g.Key, g.Sum(b => b.Count)))
            .OrderBy(b => b.StartUtc)
            .ToList();
    }

    public async Task<ChronikFilterOptions> GetFilterOptionsAsync(ClaimsPrincipal viewer, CancellationToken cancellationToken = default)
    {
        var scope = ViewerScope.From(viewer);
        if (scope.IsPartner)
        {
            return new ChronikFilterOptions(Array.Empty<string>(), Array.Empty<ChronikAgentOption>());
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var agentOpts = (await AgentDirectory.AllAsync(db, cancellationToken))
            .Select(a => new ChronikAgentOption(a.Id, a.Codename))
            .ToList();
        return new ChronikFilterOptions(RecordTypes.ToList(), agentOpts);
    }

    // ---------------------------------------------------------------- slicing

    /// <summary>Everything the SQL side of one slice needs, derived once per call.</summary>
    private sealed record SliceFilter(
        string[] RecordTypes, string[] AuditTypes, string[]? AgentIds,
        bool WantsAudit, bool WantsClassification, bool WantsScore)
    {
        public static SliceFilter From(ChronikQuery query)
        {
            var types = query.Types is { Count: > 0 } t
                ? GlobalChronikService.RecordTypes.Where(t.Contains).ToArray()
                : GlobalChronikService.RecordTypes;
            var agents = query.AgentIds is { Count: > 0 } a ? a.ToArray() : null;
            bool Wants(TimelineCategory category)
                => query.Categories is not { Count: > 0 } c || c.Contains(category);
            return new SliceFilter(
                types,
                ChronikParentResolver.AuditTypesFor(types),
                agents,
                // classification and score are the only categories the audit spine never produces;
                // asking for just those must not make the day probe chase audit-only days
                query.Categories is not { Count: > 0 } cats
                    || cats.Any(c => c is not (TimelineCategory.Classification or TimelineCategory.ThreatScore)),
                Wants(TimelineCategory.Classification),
                // score snapshots have no actor, so an agent filter rules them out entirely
                agents is null && Wants(TimelineCategory.ThreatScore));
        }

        public string[] ClassificationTypes => RecordTypes
            .Where(t => t is nameof(Person) or nameof(Faction) or nameof(PersonGroup)).ToArray();

        public string[] ScoreTypes => RecordTypes
            .Where(t => t is nameof(Person) or nameof(Faction)).ToArray();
    }

    private static IQueryable<AuditLog> AuditSlice(AppDbContext db, SliceFilter filter, DateTime fromUtc, DateTime toUtc)
    {
        var q = db.AuditLogs.Where(a => filter.AuditTypes.Contains(a.EntityType)
            && a.Timestamp >= fromUtc && a.Timestamp < toUtc);
        return filter.AgentIds is null ? q : q.Where(a => a.AgentId != null && filter.AgentIds.Contains(a.AgentId));
    }

    // newest event timestamp strictly below the cursor across all three sources; null when the window is exhausted
    private static async Task<DateTime?> NewestBeforeAsync(
        AppDbContext db, ChronikQuery query, SliceFilter filter, DateTime cursor, CancellationToken ct)
    {
        DateTime? newest = filter.WantsAudit
            ? await AuditSlice(db, filter, query.FromUtc, cursor)
                .OrderByDescending(a => a.Timestamp).Select(a => (DateTime?)a.Timestamp).FirstOrDefaultAsync(ct)
            : null;

        if (filter.WantsClassification && filter.ClassificationTypes.Length > 0)
        {
            var stamp = await ClassificationSlice(db, filter, query.FromUtc, cursor)
                .OrderByDescending(e => e.Timestamp).Select(e => (DateTime?)e.Timestamp).FirstOrDefaultAsync(ct);
            newest = Later(newest, stamp);
        }

        if (filter.WantsScore && filter.ScoreTypes.Length > 0)
        {
            var stamp = await ScoreSlice(db, filter, query.FromUtc, cursor)
                .OrderByDescending(h => h.Timestamp).Select(h => (DateTime?)h.Timestamp).FirstOrDefaultAsync(ct);
            newest = Later(newest, stamp);
        }

        return newest;

        static DateTime? Later(DateTime? a, DateTime? b)
            => a is null ? b : b is null ? a : a > b ? a : b;
    }

    private static IQueryable<ClassificationHistory> ClassificationSlice(
        AppDbContext db, SliceFilter filter, DateTime fromUtc, DateTime toUtc)
    {
        var q = db.ClassificationHistory.Where(e => filter.ClassificationTypes.Contains(e.EntityType)
            && e.Timestamp >= fromUtc && e.Timestamp < toUtc);
        return filter.AgentIds is null ? q : q.Where(e => e.AgentId != null && filter.AgentIds.Contains(e.AgentId));
    }

    private static IQueryable<ThreatScoreHistory> ScoreSlice(
        AppDbContext db, SliceFilter filter, DateTime fromUtc, DateTime toUtc)
        => db.ThreatScoreHistory.Where(h => filter.ScoreTypes.Contains(h.EntityType)
            && h.Timestamp >= fromUtc && h.Timestamp < toUtc);

    private sealed record AuditRow(
        DateTime Timestamp, string EntityType, string EntityId, AuditAction Action, string? AgentName, string? ChangesJson);

    private sealed record Pending(AuditRow Row, string ParentType, string ParentId);

    private sealed record ClassRow(DateTime Timestamp, string EntityType, string EntityId, Classification Value, string? Justification, string? AgentName);

    private sealed record ScoreRow(DateTime Timestamp, string EntityType, string EntityId, int Previous, int Current);

    private async Task<List<ChronikEvent>> LoadSliceAsync(
        AppDbContext db, ViewerScope scope, ChronikQuery query, SliceFilter filter,
        DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        // ---- 1) audit spine ----
        var auditRows = filter.WantsAudit
            ? await AuditSlice(db, filter, fromUtc, toUtc)
                .OrderByDescending(a => a.Timestamp).ThenByDescending(a => a.Id).Take(SliceCap)
                .Select(a => new AuditRow(a.Timestamp, a.EntityType, a.EntityId, a.Action, a.AgentName, a.ChangesJson))
                .ToListAsync(ct)
            : new List<AuditRow>();

        // ---- 2) child rows back to the record that owns them ----
        var childRefs = auditRows.Where(r => ChronikParentResolver.IsChild(r.EntityType))
            .Select(r => (r.EntityType, r.EntityId)).Distinct().ToList();
        var parents = await ChronikParentResolver.ResolveAsync(db, childRefs, ct);

        var pending = new List<Pending>(auditRows.Count);
        foreach (var row in auditRows)
        {
            string parentType, parentId;
            if (ChronikParentResolver.IsChild(row.EntityType))
            {
                if (!parents.TryGetValue((row.EntityType, row.EntityId), out var parent))
                {
                    continue; // owner gone, or an auto link we deliberately drop
                }
                (parentType, parentId) = (parent.Type, parent.Id);
            }
            else
            {
                (parentType, parentId) = (row.EntityType, row.EntityId);
            }
            // polymorphic children hang off any record, so the type filter can only bite here
            if (!filter.RecordTypes.Contains(parentType))
            {
                continue;
            }
            pending.Add(new Pending(row, parentType, parentId));
        }

        // ---- 3) classification history ----
        var classRows = filter.WantsClassification && filter.ClassificationTypes.Length > 0
            ? await ClassificationSlice(db, filter, fromUtc, toUtc)
                .Select(e => new ClassRow(e.Timestamp, e.EntityType, e.EntityId, e.Value, e.Justification, e.AgentName))
                .ToListAsync(ct)
            : new List<ClassRow>();

        // ---- 4) threat-score jumps ----
        var scoreRows = await ScoreJumpsAsync(db, filter, fromUtc, toUtc, ct);

        // ---- 5) detail payloads for the child events ----
        var detailRefs = pending.Where(p => ChronikParentResolver.IsChild(p.Row.EntityType))
            .Select(p => (p.Row.EntityType, p.Row.EntityId)).Distinct().ToList();
        var details = await ChronikDetails.LoadAsync(db, detailRefs, ct);

        // ---- 6) one visibility pass over every record any event names ----
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
        foreach (var p in pending) { Note(p.ParentType, p.ParentId); }
        foreach (var c in classRows) { Note(c.EntityType, c.EntityId); }
        foreach (var s in scoreRows) { Note(s.EntityType, s.EntityId); }
        foreach (var d in details.Values)
        {
            // a detail that names another record must clear the same gate as an anchor
            if (d.Reference is { } r && RecordTypes.Contains(r.Type)) { Note(r.Type, r.Id); }
        }

        var records = await ResolveVisibleAsync(db, scope, byType, ct);

        // ---- 7) build ----
        var events = new List<ChronikEvent>(pending.Count + classRows.Count + scoreRows.Count);

        foreach (var p in pending)
        {
            if (!records.TryGetValue((p.ParentType, p.ParentId), out var record))
            {
                continue;
            }
            var (category, title) = TimelineDisplay.MapAudit(p.Row.EntityType, p.Row.Action);
            var detail = details.TryGetValue((p.Row.EntityType, p.Row.EntityId), out var d) ? Render(d, records) : null;
            // the feed reads at a glance; the full before/after stays in the audit log view
            var changes = AuditDisplay.Parse(p.Row.ChangesJson, maxValueLength: 90);
            events.Add(new ChronikEvent(p.Row.Timestamp, category, p.ParentType, p.ParentId, record.Name, title,
                detail, p.Row.AgentName, Href(p.ParentType, p.ParentId), record.Deleted,
                changes.Count == 0 ? null : changes));
        }

        foreach (var c in classRows)
        {
            if (!records.TryGetValue((c.EntityType, c.EntityId), out var record))
            {
                continue;
            }
            events.Add(new ChronikEvent(c.Timestamp, TimelineCategory.Classification, c.EntityType, c.EntityId,
                record.Name, $"Einstufung: {ClassificationDisplay.Name(c.Value)}",
                TimelineDisplay.Truncate(c.Justification), c.AgentName,
                Href(c.EntityType, c.EntityId), record.Deleted));
        }

        foreach (var s in scoreRows)
        {
            if (!records.TryGetValue((s.EntityType, s.EntityId), out var record))
            {
                continue;
            }
            var arrow = s.Current > s.Previous ? "gestiegen" : "gefallen";
            events.Add(new ChronikEvent(s.Timestamp, TimelineCategory.ThreatScore, s.EntityType, s.EntityId,
                record.Name, $"Bedrohungs-Score {arrow}: {s.Previous} → {s.Current}", null, null,
                Href(s.EntityType, s.EntityId), record.Deleted));
        }

        return Postfilter(events, query);
    }

    // category and free text can only be applied once names and details exist
    private static List<ChronikEvent> Postfilter(List<ChronikEvent> events, ChronikQuery query)
    {
        IEnumerable<ChronikEvent> result = events;
        if (query.Categories is { Count: > 0 } categories)
        {
            result = result.Where(e => categories.Contains(e.Category));
        }
        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            var needle = query.Text.Trim();
            result = result.Where(e =>
                e.Name.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || e.Title.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || (e.Detail?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false)
                || (e.ActorName?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        return result.ToList();
    }

    private static string? Render(ChronikDetails.Detail detail, Dictionary<(string, string), RecordRef> records)
    {
        if (detail.Reference is not { } reference)
        {
            return detail.Text;
        }
        var name = records.TryGetValue(reference, out var record) ? record.Name : HiddenRecord;
        return $"{detail.Prefix}{name}{detail.Suffix}";
    }

    // history rows only exist where the score actually changed, so consecutive rows per record are the jumps
    private static async Task<List<ScoreRow>> ScoreJumpsAsync(
        AppDbContext db, SliceFilter filter, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        if (!filter.WantsScore || filter.ScoreTypes.Length == 0)
        {
            return new List<ScoreRow>();
        }

        // reach back far enough to find the predecessor of the slice's oldest snapshot
        var lookback = fromUtc.AddDays(-90);
        var rows = await db.ThreatScoreHistory
            .Where(h => filter.ScoreTypes.Contains(h.EntityType) && h.Timestamp >= lookback && h.Timestamp < toUtc)
            .Select(h => new { h.EntityType, h.EntityId, h.Score, h.Timestamp })
            .ToListAsync(ct);

        var jumps = new List<ScoreRow>();
        foreach (var group in rows.GroupBy(h => (h.EntityType, h.EntityId)))
        {
            var ordered = group.OrderBy(h => h.Timestamp).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                var previous = ordered[i - 1].Score;
                var current = ordered[i].Score;
                var at = ordered[i].Timestamp;
                if (previous is null || current is null || at < fromUtc
                    || Math.Abs(current.Value - previous.Value) < ScoreJumpThreshold)
                {
                    continue;
                }
                jumps.Add(new ScoreRow(at, group.Key.EntityType, group.Key.EntityId, previous.Value, current.Value));
            }
        }
        return jumps;
    }

    // ---------------------------------------------------------------- visibility

    /// <summary>Display name plus trash state of a record the viewer may see.</summary>
    private readonly record struct RecordRef(string Name, bool Deleted);

    // resolves names ONLY for records the viewer may see; IgnoreQueryFilters so deletion/restore events survive
    private static async Task<Dictionary<(string Type, string Id), RecordRef>> ResolveVisibleAsync(
        AppDbContext db, ViewerScope scope, Dictionary<string, HashSet<string>> byType, CancellationToken ct)
    {
        var result = new Dictionary<(string, string), RecordRef>();
        List<string> Ids(string type) => byType.TryGetValue(type, out var s) ? s.ToList() : new List<string>();

        bool VisRestricted(bool c, bool tru, bool hrb) => scope.CanSee(
            !c ? DocumentClassification.None : tru ? DocumentClassification.Tru : hrb ? DocumentClassification.Hrb : DocumentClassification.Leadership);
        bool VisDocument(bool c, bool tru, bool hrb) => scope.CanSee(
            c ? DocumentClassification.Leadership : tru ? DocumentClassification.Tru : hrb ? DocumentClassification.Hrb : DocumentClassification.None);

        var personIds = Ids(nameof(Person));
        if (personIds.Count > 0)
        {
            foreach (var x in await db.People.IgnoreQueryFilters().Where(p => personIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name, p.IsClassified, p.IsTRUClassified, p.IsHRBClassified, p.IsDeleted }).ToListAsync(ct))
            {
                if (VisRestricted(x.IsClassified, x.IsTRUClassified, x.IsHRBClassified)) { result[(nameof(Person), x.Id)] = new RecordRef(x.Name, x.IsDeleted); }
            }
        }

        var factionIds = Ids(nameof(Faction));
        if (factionIds.Count > 0)
        {
            foreach (var x in await db.Factions.IgnoreQueryFilters().Where(f => factionIds.Contains(f.Id))
                .Select(f => new { f.Id, f.Name, f.IsClassified, f.IsTRUClassified, f.IsHRBClassified, f.IsDeleted }).ToListAsync(ct))
            {
                if (VisRestricted(x.IsClassified, x.IsTRUClassified, x.IsHRBClassified)) { result[(nameof(Faction), x.Id)] = new RecordRef(x.Name, x.IsDeleted); }
            }
        }

        var groupIds = Ids(nameof(PersonGroup));
        if (groupIds.Count > 0)
        {
            foreach (var x in await db.PersonGroups.IgnoreQueryFilters().Where(g => groupIds.Contains(g.Id))
                .Select(g => new { g.Id, g.Name, g.IsClassified, g.IsTRUClassified, g.IsHRBClassified, g.IsDeleted }).ToListAsync(ct))
            {
                if (VisRestricted(x.IsClassified, x.IsTRUClassified, x.IsHRBClassified)) { result[(nameof(PersonGroup), x.Id)] = new RecordRef(x.Name, x.IsDeleted); }
            }
        }

        var partyIds = Ids(nameof(Party));
        if (partyIds.Count > 0)
        {
            foreach (var x in await db.Parties.IgnoreQueryFilters().Where(p => partyIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name, p.IsClassified, p.IsTRUClassified, p.IsHRBClassified, p.IsDeleted }).ToListAsync(ct))
            {
                if (VisRestricted(x.IsClassified, x.IsTRUClassified, x.IsHRBClassified)) { result[(nameof(Party), x.Id)] = new RecordRef(x.Name, x.IsDeleted); }
            }
        }

        var operationIds = Ids(nameof(Operation));
        if (operationIds.Count > 0)
        {
            foreach (var x in await db.Operations.IgnoreQueryFilters().Where(o => operationIds.Contains(o.Id))
                .Select(o => new { o.Id, o.Title, o.IsClassified, o.IsTRUClassified, o.IsHRBClassified, o.IsDeleted }).ToListAsync(ct))
            {
                if (VisRestricted(x.IsClassified, x.IsTRUClassified, x.IsHRBClassified)) { result[(nameof(Operation), x.Id)] = new RecordRef(x.Title, x.IsDeleted); }
            }
        }

        var caseIds = Ids(nameof(Case));
        if (caseIds.Count > 0)
        {
            foreach (var x in await db.Cases.IgnoreQueryFilters().Where(v => caseIds.Contains(v.Id))
                .Select(v => new { v.Id, v.Title, v.IsClassified, v.IsTRUClassified, v.IsHRBClassified, v.IsDeleted }).ToListAsync(ct))
            {
                if (VisRestricted(x.IsClassified, x.IsTRUClassified, x.IsHRBClassified)) { result[(nameof(Case), x.Id)] = new RecordRef(x.Title, x.IsDeleted); }
            }
        }

        var documentIds = Ids(nameof(Document));
        if (documentIds.Count > 0)
        {
            foreach (var x in await db.Documents.IgnoreQueryFilters().Where(d => documentIds.Contains(d.Id))
                .Select(d => new { d.Id, d.Title, d.IsClassified, d.IsTRUClassified, d.IsHRBClassified, d.IsDeleted }).ToListAsync(ct))
            {
                if (VisDocument(x.IsClassified, x.IsTRUClassified, x.IsHRBClassified))
                {
                    result[(nameof(Document), x.Id)] = new RecordRef(string.IsNullOrWhiteSpace(x.Title) ? "Dokument" : x.Title, x.IsDeleted);
                }
            }
        }

        var lawIds = Ids(nameof(Law));
        if (lawIds.Count > 0)
        {
            foreach (var x in await db.Laws.IgnoreQueryFilters().Where(g => lawIds.Contains(g.Id))
                .Select(g => new { g.Id, g.Paragraph, g.Title, g.IsDeleted }).ToListAsync(ct))
            {
                result[(nameof(Law), x.Id)] = new RecordRef($"{x.Paragraph} {x.Title}".Trim(), x.IsDeleted);
            }
        }

        var tfIds = Ids(nameof(Taskforce));
        if (tfIds.Count > 0)
        {
            var visible = await TaskforceVisibility.VisibleIdsAsync(db, tfIds, scope.MayAllTaskforces, scope.MeId, ct);
            if (visible.Count > 0)
            {
                foreach (var x in await db.Taskforces.IgnoreQueryFilters().Where(t => visible.Contains(t.Id))
                    .Select(t => new { t.Id, t.Name, t.IsDeleted }).ToListAsync(ct))
                {
                    result[(nameof(Taskforce), x.Id)] = new RecordRef(x.Name, x.IsDeleted);
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
                    .Select(a => new { a.Id, a.Title, a.IsDeleted }).ToListAsync(ct))
                {
                    result[(nameof(Job), x.Id)] = new RecordRef(x.Title, x.IsDeleted);
                }
            }
        }

        return result;
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>Start of the local day that contains the given UTC instant, back in UTC.</summary>
    private static DateTime LocalDayStartUtc(DateTime utc)
    {
        var local = DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();
        var start = new DateTime(local.Year, local.Month, local.Day, 0, 0, 0, DateTimeKind.Local);
        return start.ToUniversalTime();
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
