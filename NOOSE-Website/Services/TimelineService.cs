using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Jobs;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.Groups;
using NOOSE_Website.Data.Entities.Operations;
using NOOSE_Website.Data.Entities.Parties;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Data.Entities.Appointments;
using NOOSE_Website.Data.Entities.Cases;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Timeline;

namespace NOOSE_Website.Services;

/// <summary>Builds unified timeline from audit and semantic sources.</summary>
public class TimelineService(IDbContextFactory<AppDbContext> dbFactory) : ITimelineService
{
    // internal DTO
    private sealed record Raw(
        DateTime Timestamp, TimelineCategory Category, string Title, string? Detail,
        string? ActorName, string? ActorId, string? Href,
        IReadOnlyList<AuditDisplay.FieldChange>? Changes);

    public async Task<IReadOnlyList<TimelineEntry>> GetTimelineAsync(
        string entityType, string entityId, ClaimsPrincipal viewer,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var scope = ViewerScope.From(viewer);
        var agency = scope.PartnerAgency;
        var isPartner = agency is not null;
        var mayClassified = viewer.MayClassifiedRead();
        var mayAllTf = viewer.MayAllTaskforcesSee();
        var meId = viewer.GetAgentId();

        // visibility gate
        if (!await Visibility.IsRecordVisibleAsync(db, entityType, entityId, scope, cancellationToken))
        {
            return Array.Empty<TimelineEntry>();
        }
        // check job restriction
        if (entityType == nameof(Job))
        {
            var visible = await JobVisibility.VisibleIdsAsync(db, new[] { entityId }, mayAllTf, meId, cancellationToken);
            if (!visible.Contains(entityId))
            {
                return Array.Empty<TimelineEntry>();
            }
        }
        // check appointment restriction
        if (entityType == nameof(Appointment))
        {
            var visible = await AppointmentVisibility.VisibleIdsAsync(db, new[] { entityId }, mayAllTf, meId, cancellationToken);
            if (!visible.Contains(entityId))
            {
                return Array.Empty<TimelineEntry>();
            }
        }

        var raw = new List<Raw>();
        var actorIds = new HashSet<string>();

        // ---- 1) audit base ----
        string[] types;
        HashSet<string> auditIds;
        if (isPartner)
        {
            // record-self audit only, no child fan-out
            types = new[] { entityType };
            auditIds = new HashSet<string> { entityId };
        }
        else
        {
            (types, auditIds) = await AuditSourceAsync(db, entityType, entityId, cancellationToken);
        }
        foreach (var log in await db.AuditLogs
            .Where(a => types.Contains(a.EntityType) && auditIds.Contains(a.EntityId)
                && a.EntityType != nameof(Link))
            .Select(a => new { a.Timestamp, a.EntityType, a.Action, a.ChangesJson, a.AgentName })
            .ToListAsync(cancellationToken))
        {
            var (kat, title) = MapAudit(log.EntityType, log.Action);
            raw.Add(new Raw(log.Timestamp, kat, title, null, log.AgentName, null, null,
                AuditDisplay.Parse(log.ChangesJson)));
        }

        // ---- 2) classification history ----
        foreach (var e in await db.ClassificationHistory
            .Where(e => e.EntityType == entityType && e.EntityId == entityId)
            .Select(e => new { e.Value, e.Justification, e.Timestamp, e.AgentName })
            .ToListAsync(cancellationToken))
        {
            raw.Add(new Raw(e.Timestamp, TimelineCategory.Classification,
                $"Einstufung: {ClassificationDisplay.Name(e.Value)}", e.Justification, e.AgentName, null, null, null));
        }

        // ---- 3) comments ----
        var comments = await db.Comments
            .Where(k => k.EntityType == entityType && k.EntityId == entityId)
            .Select(k => new { k.Id, k.Text, k.AuthorName, k.CreatedAt })
            .ToListAsync(cancellationToken);
        if (isPartner)
        {
            comments = await PartnerVisibility.FilterChildrenAsync(db, entityType, entityId, nameof(Comment), comments, k => k.Id, agency!.Value, meId, cancellationToken);
        }
        foreach (var k in comments)
        {
            raw.Add(new Raw(k.CreatedAt, TimelineCategory.Comment,
                "Kommentar", Truncate(k.Text), k.AuthorName, null, null, null));
        }

        // ---- 4) sources/attachments ----
        var sources = await db.Sources
            .Where(q => q.EntityType == entityType && q.EntityId == entityId)
            .Select(q => new { q.Id, q.Title, q.Type, q.CreatedAt, q.CreatedById })
            .ToListAsync(cancellationToken);
        if (isPartner)
        {
            // drop cross-ref source types, then child-release filter
            sources = sources.Where(q => q.Type != SourceType.Internal && q.Type != SourceType.Document).ToList();
            sources = await PartnerVisibility.FilterChildrenAsync(db, entityType, entityId, nameof(Source), sources, q => q.Id, agency!.Value, meId, cancellationToken);
        }
        foreach (var q in sources)
        {
            Remember(actorIds, q.CreatedById);
            var title = string.IsNullOrWhiteSpace(q.Title) ? "Quelle hinzugefügt" : $"Quelle hinzugefügt: {q.Title}";
            raw.Add(new Raw(q.CreatedAt, TimelineCategory.Source, title,
                SourceTypeDisplay.Name(q.Type), null, q.CreatedById, null, null));
        }

        // ---- 5) followups ----
        var followups = await db.Followups
            .Where(w => w.EntityType == entityType && w.EntityId == entityId)
            .Select(w => new { w.Id, w.DueAt, w.Note, w.Done, w.DoneAt, w.DoneById, w.CreatedAt, w.CreatedById })
            .ToListAsync(cancellationToken);
        if (isPartner)
        {
            followups = await PartnerVisibility.FilterChildrenAsync(db, entityType, entityId, nameof(Followup), followups, w => w.Id, agency!.Value, meId, cancellationToken);
        }
        foreach (var w in followups)
        {
            Remember(actorIds, w.CreatedById);
            var due = w.DueAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
            var detail = string.IsNullOrWhiteSpace(w.Note) ? $"fällig am {due}" : $"fällig am {due} · {w.Note}";
            raw.Add(new Raw(w.CreatedAt, TimelineCategory.Followup, "Wiedervorlage angelegt",
                detail, null, w.CreatedById, null, null));
            if (w.Done && w.DoneAt is { } done)
            {
                Remember(actorIds, w.DoneById);
                raw.Add(new Raw(done, TimelineCategory.Followup, "Wiedervorlage erledigt",
                    w.Note, null, w.DoneById, null, null));
            }
        }

        // ---- 6) links ----
        // cross-ref: skip for partners
        var link = isPartner ? null : await db.Links.IgnoreQueryFilters()
            .Where(v => !v.Automatic
                && ((v.SourceType == entityType && v.SourceId == entityId)
                 || (v.TargetType == entityType && v.TargetId == entityId)))
            .Select(v => new { v.SourceType, v.SourceId, v.TargetType, v.TargetId, v.Label, v.CreatedAt, v.CreatedById, v.IsDeleted, v.DeletedAt, v.DeletedById })
            .ToListAsync(cancellationToken);
        if (link is { Count: > 0 })
        {
            (string, string) Counterpart(string sourceType, string sourceId, string targetType, string targetId)
                => sourceType == entityType && sourceId == entityId ? (targetType, targetId) : (sourceType, sourceId);

            var refs = link.Select(v => Counterpart(v.SourceType, v.SourceId, v.TargetType, v.TargetId)).Distinct().ToList();
            var map = await RecordsReference.ResolveAsync(db, refs, cancellationToken, mayAllTf, meId);
            foreach (var v in link)
            {
                var (name, href) = CounterpartDisplay(map, Counterpart(v.SourceType, v.SourceId, v.TargetType, v.TargetId), "Akte");
                Remember(actorIds, v.CreatedById);
                raw.Add(new Raw(v.CreatedAt, TimelineCategory.Link, $"Verknüpft mit {name}",
                    v.Label, null, v.CreatedById, href, null));
                if (v.IsDeleted && v.DeletedAt is { } removed)
                {
                    Remember(actorIds, v.DeletedById);
                    raw.Add(new Raw(removed, TimelineCategory.Link, $"Verknüpfung mit {name} entfernt",
                        v.Label, null, v.DeletedById, href, null));
                }
            }
        }

        // ---- 7) person-specific ----
        if (entityType == nameof(Person))
        {
            var observations = await db.Observations
                .Where(o => o.PersonId == entityId)
                .Select(o => new { o.Id, o.Start, o.Location, o.Sighting, o.CreatedById })
                .ToListAsync(cancellationToken);
            if (isPartner)
            {
                observations = await PartnerVisibility.FilterChildrenAsync(db, nameof(Person), entityId, nameof(Observation), observations, o => o.Id, agency!.Value, meId, cancellationToken);
            }
            foreach (var o in observations)
            {
                Remember(actorIds, o.CreatedById);
                var title = string.IsNullOrWhiteSpace(o.Location) ? "Observation" : $"Observation – {o.Location}";
                raw.Add(new Raw(o.Start, TimelineCategory.Observation, title,
                    Truncate(o.Sighting), null, o.CreatedById, null, null));
            }

            var photos = await db.PersonPhotos
                .Where(f => f.PersonId == entityId)
                .Select(f => new { f.Id, f.OriginalName, f.CreatedAt, f.CreatedById })
                .ToListAsync(cancellationToken);
            if (isPartner)
            {
                photos = await PartnerVisibility.FilterChildrenAsync(db, nameof(Person), entityId, nameof(PersonPhoto), photos, f => f.Id, agency!.Value, meId, cancellationToken);
            }
            foreach (var f in photos)
            {
                Remember(actorIds, f.CreatedById);
                raw.Add(new Raw(f.CreatedAt, TimelineCategory.Photo, "Foto hinzugefügt",
                    f.OriginalName, null, f.CreatedById, null, null));
            }

            // cross-ref: skip for partners
            var bez = isPartner ? null : await db.PersonRelations
                .Where(b => b.PersonAId == entityId || b.PersonBId == entityId)
                .Select(b => new { b.PersonAId, b.PersonBId, b.Type, b.Note, b.CreatedAt, b.CreatedById })
                .ToListAsync(cancellationToken);
            if (bez is { Count: > 0 })
            {
                var refs = bez.Select(b => (nameof(Person), b.PersonAId == entityId ? b.PersonBId : b.PersonAId))
                    .Distinct().ToList();
                var map = await RecordsReference.ResolveAsync(db, refs, cancellationToken, mayAllTf, meId);
                foreach (var b in bez)
                {
                    var otherId = b.PersonAId == entityId ? b.PersonBId : b.PersonAId;
                    var (name, href) = CounterpartDisplay(map, (nameof(Person), otherId), "Person");
                    Remember(actorIds, b.CreatedById);
                    raw.Add(new Raw(b.CreatedAt, TimelineCategory.Relation,
                        $"Beziehung ({RelationTypeDisplay.Name(b.Type)}): {name}", b.Note, null, b.CreatedById, href, null));
                }
            }
        }

        // ---- 8) faction-specific ----
        if (entityType == nameof(Faction))
        {
            var activities = await db.FactionActivities
                .Where(a => a.FactionId == entityId)
                .Select(a => new { a.Id, a.Title, a.Kind, a.Timestamp, a.Description, a.Location, a.CreatedById })
                .ToListAsync(cancellationToken);
            if (isPartner)
            {
                activities = await PartnerVisibility.FilterChildrenAsync(db, nameof(Faction), entityId, nameof(FactionActivity), activities, a => a.Id, agency!.Value, meId, cancellationToken);
            }
            foreach (var a in activities)
            {
                Remember(actorIds, a.CreatedById);
                var title = string.IsNullOrWhiteSpace(a.Kind) ? $"Aktivität: {a.Title}" : $"Aktivität ({a.Kind}): {a.Title}";
                var detail = a.Description;
                if (!string.IsNullOrWhiteSpace(a.Location))
                {
                    detail = string.IsNullOrWhiteSpace(detail) ? $"Ort: {a.Location}" : $"{detail} · Ort: {a.Location}";
                }
                raw.Add(new Raw(a.Timestamp, TimelineCategory.Activity, title, detail, null, a.CreatedById, null, null));
            }
        }

        // ---- resolve actor names ----
        var names = actorIds.Count == 0
            ? new Dictionary<string, string?>()
            : await db.Users.Where(u => actorIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Codename })
                .ToDictionaryAsync(u => u.Id, u => (string?)u.Codename, cancellationToken);

        return raw
            .Select(r => new TimelineEntry(r.Timestamp, r.Category, r.Title, r.Detail,
                ActorName(r, names), r.Href, r.Changes))
            .OrderByDescending(e => e.Timestamp)
            .ToList();
    }

    // audit sources
    private static async Task<(string[] Types, HashSet<string> Ids)> AuditSourceAsync(
        AppDbContext db, string type, string id, CancellationToken ct)
    {
        var ids = new HashSet<string> { id };
        switch (type)
        {
            case nameof(Person):
                ids.UnionWith(await db.PersonDocs.IgnoreQueryFilters().Where(d => d.PersonId == id).Select(d => d.Id).ToListAsync(ct));
                return ([nameof(Person), nameof(PersonDoc)], ids);
            case nameof(Faction):
                ids.UnionWith(await db.FactionMembers.IgnoreQueryFilters().Where(m => m.FactionId == id).Select(m => m.Id).ToListAsync(ct));
                ids.UnionWith(await db.FactionAgents.IgnoreQueryFilters().Where(a => a.FactionId == id).Select(a => a.Id).ToListAsync(ct));
                return ([nameof(Faction), nameof(FactionMember), nameof(FactionAgent)], ids);
            case nameof(PersonGroup):
                ids.UnionWith(await db.PersonGroupMembers.IgnoreQueryFilters().Where(m => m.PersonGroupId == id).Select(m => m.Id).ToListAsync(ct));
                ids.UnionWith(await db.PersonGroupAgents.IgnoreQueryFilters().Where(a => a.PersonGroupId == id).Select(a => a.Id).ToListAsync(ct));
                return ([nameof(PersonGroup), nameof(PersonGroupMember), nameof(PersonGroupAgent)], ids);
            case nameof(Party):
                ids.UnionWith(await db.PartyMembers.IgnoreQueryFilters().Where(m => m.PartyId == id).Select(m => m.Id).ToListAsync(ct));
                ids.UnionWith(await db.PartyAgents.IgnoreQueryFilters().Where(a => a.PartyId == id).Select(a => a.Id).ToListAsync(ct));
                return ([nameof(Party), nameof(PartyMember), nameof(PartyAgent)], ids);
            case nameof(Operation):
                ids.UnionWith(await db.OperationAgents.IgnoreQueryFilters().Where(a => a.OperationId == id).Select(a => a.Id).ToListAsync(ct));
                return ([nameof(Operation), nameof(OperationAgent)], ids);
            case nameof(Case):
                ids.UnionWith(await db.CaseAgents.IgnoreQueryFilters().Where(a => a.CaseId == id).Select(a => a.Id).ToListAsync(ct));
                return ([nameof(Case), nameof(CaseAgent)], ids);
            case nameof(Taskforce):
                ids.UnionWith(await db.TaskforceAgents.IgnoreQueryFilters().Where(a => a.TaskforceId == id).Select(a => a.Id).ToListAsync(ct));
                return ([nameof(Taskforce), nameof(TaskforceAgent)], ids);
            case nameof(Job):
                ids.UnionWith(await db.JobAssignments.IgnoreQueryFilters().Where(z => z.JobId == id).Select(z => z.Id).ToListAsync(ct));
                return ([nameof(Job), nameof(JobAssignment)], ids);
            case nameof(Appointment):
                ids.UnionWith(await db.AppointmentAssignments.IgnoreQueryFilters().Where(z => z.AppointmentId == id).Select(z => z.Id).ToListAsync(ct));
                return ([nameof(Appointment), nameof(AppointmentAssignment)], ids);
            default:
                return ([type], ids);
        }
    }

    // map audit entry
    private static (TimelineCategory Kat, string Title) MapAudit(string entityType, AuditAction action)
    {
        string Verb(string created, string deleted) => action switch
        {
            AuditAction.Created => created,
            AuditAction.Modified => "geändert",
            AuditAction.Deleted => deleted,
            AuditAction.Restored => "wiederhergestellt",
            _ => action.ToString(),
        };

        if (entityType == nameof(PersonDoc))
        {
            return (TimelineCategory.Doc, $"Dok {Verb("angelegt", "gelöscht")}");
        }
        if (entityType is nameof(FactionMember) or nameof(PersonGroupMember) or nameof(PartyMember))
        {
            return (TimelineCategory.Membership, $"Mitglied {Verb("aufgenommen", "entfernt")}");
        }
        if (entityType is nameof(FactionAgent) or nameof(PersonGroupAgent) or nameof(PartyAgent)
            or nameof(OperationAgent) or nameof(CaseAgent) or nameof(TaskforceAgent) or nameof(JobAssignment)
            or nameof(AppointmentAssignment))
        {
            return (TimelineCategory.Allocation, $"Agent {Verb("zugeteilt", "entfernt")}");
        }

        var kat = action switch
        {
            AuditAction.Created => TimelineCategory.Asset,
            AuditAction.Deleted => TimelineCategory.Deletion,
            AuditAction.Restored => TimelineCategory.Restoration,
            _ => TimelineCategory.Change,
        };
        return (kat, $"Akte {Verb("angelegt", "gelöscht")}");
    }

    // resolve target
    private static (string Name, string? Href) CounterpartDisplay(
        Dictionary<(string, string), RecordsReference.Resolution> map, (string, string) target, string covertNoun)
    {
        if (!map.TryGetValue(target, out var on))
        {
            return ($"nicht verfügbare {covertNoun}", null);
        }
        return on.Classified ? ($"verdeckte {covertNoun}", null) : (on.Display, on.Href);
    }

    private static void Remember(HashSet<string> ids, string? id)
    {
        if (!string.IsNullOrEmpty(id))
        {
            ids.Add(id);
        }
    }

    private static string? ActorName(Raw r, Dictionary<string, string?> names)
    {
        if (!string.IsNullOrWhiteSpace(r.ActorName))
        {
            return r.ActorName;
        }
        if (r.ActorId is { } id && names.TryGetValue(id, out var name) && !string.IsNullOrWhiteSpace(name))
        {
            return name;
        }
        return null;
    }

    private static string? Truncate(string? text, int max = 160)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        text = text.Trim();
        return text.Length <= max ? text : string.Concat(text.AsSpan(0, max), "…");
    }
}
