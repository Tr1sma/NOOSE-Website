using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Jobs;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.Operations;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Appointments;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Calendar;

namespace NOOSE_Website.Services;

/// <summary>Builds calendar entries for a time window in two views.</summary>
public class CalendarService(IDbContextFactory<AppDbContext> dbFactory) : ICalendarService
{
    private const int PerSourceMax = 500;

    public async Task<IReadOnlyList<CalendarEntry>> GetEntriesAsync(
        DateTime sourceUtc, DateTime untilUtc, ClaimsPrincipal viewer, CalendarMode mode, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var mayClassified = viewer.MayClassifiedRead();
        var meId = viewer.GetAgentId();
        var entries = new List<CalendarEntry>();

        if (mode == CalendarMode.My)
        {
            await LoadMyAsync(db, sourceUtc, untilUtc, mayClassified, meId, entries, cancellationToken);
        }
        else
        {
            await LoadAuthorityAsync(db, sourceUtc, untilUtc, mayClassified, meId, entries, cancellationToken);
        }

        return entries;
    }

    // ---- my calendar ----
    private async Task LoadMyAsync(AppDbContext db, DateTime sourceUtc, DateTime untilUtc, bool mayClassified, string? meId,
        List<CalendarEntry> entries, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(meId))
        {
            return; // no agent context
        }

        // own appointments
        foreach (var t in await db.Appointments.OnlyOwn(db, meId)
            .Where(t => t.Start <= untilUtc && (t.End ?? t.Start) >= sourceUtc)
            .OrderBy(t => t.Start).Take(PerSourceMax)
            .Select(t => new { t.Id, t.Title, t.Start, t.End, t.AllDay, t.Status })
            .ToListAsync(ct))
        {
            entries.Add(new CalendarEntry($"tm:{t.Id}", t.Title, Local(t.Start), LocalOpt(t.End),
                t.AllDay, CalendarSource.Appointment, $"/kalender/{t.Id}", AppointmentStatusDisplay.IsObsolete(t.Status),
                nameof(Appointment), t.Id));
        }

        // assigned jobs
        foreach (var a in await db.Jobs
            .Where(a => (a.CreatedById == meId || db.JobAssignments.Any(z => z.JobId == a.Id && z.AgentId == meId))
                && a.DueDate != null && a.DueDate >= sourceUtc && a.DueDate <= untilUtc)
            .OrderBy(a => a.DueDate).Take(PerSourceMax)
            .Select(a => new { a.Id, a.Title, a.DueDate, a.Status })
            .ToListAsync(ct))
        {
            entries.Add(new CalendarEntry($"auf:{a.Id}", a.Title, Local(a.DueDate!.Value), null,
                false, CalendarSource.Job, $"/aufgaben/{a.Id}", JobStatusDisplay.IsCompleted(a.Status),
                nameof(Job), a.Id));
        }

        // open followups
        var wvs = await db.Followups
            .Where(w => !w.Done && (w.ResponsibleAgentId == meId || w.CreatedById == meId)
                && w.DueAt >= sourceUtc && w.DueAt <= untilUtc)
            .OrderBy(w => w.DueAt).Take(PerSourceMax)
            .Select(w => new { w.Id, w.Note, w.DueAt, w.EntityType, w.EntityId })
            .ToListAsync(ct);
        if (wvs.Count > 0)
        {
            var refs = wvs.Select(w => (w.EntityType, w.EntityId)).Distinct().ToList();
            var map = await RecordsReference.ResolveAsync(db, refs, ct, mayClassified, meId);
            foreach (var w in wvs)
            {
                map.TryGetValue((w.EntityType, w.EntityId), out var parents);
                // always show; hide classified parent
                var mayName = parents.Display is not null && !(parents.Classified && !mayClassified);
                var @base = mayName ? $"Wiedervorlage: {parents.Display}" : "Wiedervorlage fällig";
                var title = string.IsNullOrWhiteSpace(w.Note) ? @base : $"{@base} · {w.Note}";
                entries.Add(new CalendarEntry($"wv:{w.Id}", title, Local(w.DueAt), null,
                    false, CalendarSource.Followup, mayName ? parents.Href : null,
                    // same gate as the title and the link: a classified parent is not named, not linked, not referenced
                    EntityType: mayName ? w.EntityType : null, EntityId: mayName ? w.EntityId : null));
            }
        }

        // zone conversion is not translatable
        var fromDay = MeetingTime.Day(sourceUtc);
        var untilDay = MeetingTime.Day(untilUtc);

        // meetings
        var meetings = await db.Meetings
            .Where(m => m.Start <= untilUtc && (m.End ?? m.Start) >= sourceUtc)
            .OrderBy(m => m.Start).Take(PerSourceMax)
            .Select(m => new { m.Id, m.Title, m.Start, m.End, m.Status })
            .ToListAsync(ct);
        if (meetings.Count > 0)
        {
            var ids = meetings.Select(m => m.Id).ToList();
            // uncapped: a dropped row would falsely un-excuse
            var signedOff = (await db.MeetingSignOffs
                .Where(s => s.AgentId == meId && ids.Contains(s.MeetingId))
                .Select(s => s.MeetingId)
                .ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
            var excusedSpans = await db.Absences
                .Where(a => a.AgentId == meId && a.FromDate <= untilDay && a.ToDate >= fromDay)
                .Select(a => new { a.FromDate, a.ToDate })
                .ToListAsync(ct);
            foreach (var m in meetings)
            {
                var day = MeetingTime.Day(m.Start);
                // sign-off and absence both excuse; the meeting page shows the same
                var excused = signedOff.Contains(m.Id)
                    || excusedSpans.Any(a => a.FromDate <= day && a.ToDate >= day);
                entries.Add(new CalendarEntry($"bes:{m.Id}", m.Title, Local(m.Start), LocalOpt(m.End),
                    false, CalendarSource.Meeting, $"/besprechungen/{m.Id}",
                    excused || MeetingStatusDisplay.IsObsolete(m.Status),
                    nameof(Data.Entities.Meetings.Meeting), m.Id));
            }
        }

        // own absences
        foreach (var ab in await db.Absences
            .Where(ab => ab.AgentId == meId && ab.FromDate <= untilDay && ab.ToDate >= fromDay)
            .OrderBy(ab => ab.FromDate).Take(PerSourceMax)
            .Select(ab => new { ab.Id, ab.FromDate, ab.ToDate, ab.Category })
            .ToListAsync(ct))
        {
            // already unspecified; Local() would shift a second time
            entries.Add(new CalendarEntry($"abm:{ab.Id}", $"Abgemeldet: {AbsenceCategoryDisplay.Name(ab.Category)}",
                ab.FromDate.ToDateTime(TimeOnly.MinValue), ab.ToDate.ToDateTime(TimeOnly.MinValue),
                true, CalendarSource.Absence, "/abmeldungen"));
        }

        // other agents' absences live in the authority calendar, not here
    }

    // ---- authority calendar ----
    private async Task LoadAuthorityAsync(AppDbContext db, DateTime sourceUtc, DateTime untilUtc, bool mayClassified,
        string? meId, List<CalendarEntry> entries, CancellationToken ct)
    {
        // public appointments
        foreach (var t in await db.Appointments.ForAuthority(mayClassified)
            .Where(t => t.Start <= untilUtc && (t.End ?? t.Start) >= sourceUtc)
            .OrderBy(t => t.Start).Take(PerSourceMax)
            .Select(t => new { t.Id, t.Title, t.Start, t.End, t.AllDay, t.Status })
            .ToListAsync(ct))
        {
            entries.Add(new CalendarEntry($"tm:{t.Id}", t.Title, Local(t.Start), LocalOpt(t.End),
                t.AllDay, CalendarSource.Appointment, $"/kalender/{t.Id}", AppointmentStatusDisplay.IsObsolete(t.Status),
                nameof(Appointment), t.Id));
        }

        // operations
        foreach (var o in await db.Operations
            .Where(o => (mayClassified || !o.IsClassified)
                && o.Start != null && o.Start <= untilUtc && (o.End ?? o.Start) >= sourceUtc)
            .OrderBy(o => o.Start).Take(PerSourceMax)
            .Select(o => new { o.Id, o.Title, o.Start, o.End, o.Status })
            .ToListAsync(ct))
        {
            entries.Add(new CalendarEntry($"op:{o.Id}", o.Title, Local(o.Start!.Value), LocalOpt(o.End),
                false, CalendarSource.Operation, $"/operationen/{o.Id}", o.Status == OperationStatus.Aborted,
                nameof(Operation), o.Id));
        }

        // observations
        foreach (var ob in await db.Observations
            .Where(ob => (mayClassified || !ob.Person!.IsClassified)
                && ob.Start <= untilUtc && (ob.End ?? ob.Start) >= sourceUtc)
            .OrderBy(ob => ob.Start).Take(PerSourceMax)
            .Select(ob => new { ob.Id, ob.Location, ob.Start, ob.End, ob.PersonId })
            .ToListAsync(ct))
        {
            var title = string.IsNullOrWhiteSpace(ob.Location) ? "Observation" : $"Observation – {ob.Location}";
            // the observation has no page of its own, so the record it belongs to is the person
            entries.Add(new CalendarEntry($"ob:{ob.Id}", title, Local(ob.Start), LocalOpt(ob.End),
                false, CalendarSource.Observation, $"/personen/{ob.PersonId}", false,
                nameof(Person), ob.PersonId));
        }

        // person docs
        foreach (var d in await db.PersonDocs
            .Where(d => (mayClassified || !d.Person!.IsClassified)
                && d.Timestamp >= sourceUtc && d.Timestamp <= untilUtc)
            .OrderBy(d => d.Timestamp).Take(PerSourceMax)
            .Select(d => new { d.Id, d.Timestamp, d.Reason, d.PersonId, PersonName = d.Person!.Name })
            .ToListAsync(ct))
        {
            var title = string.IsNullOrWhiteSpace(d.Reason) ? $"Dok: {d.PersonName}" : $"Dok: {d.PersonName} – {Truncate(d.Reason!)}";
            entries.Add(new CalendarEntry($"dok:{d.Id}", title, Local(d.Timestamp), null,
                false, CalendarSource.PersonDoc, $"/personen/{d.PersonId}?tab=doks", false,
                nameof(Person), d.PersonId));
        }

        // faction activities (AgentActivities linked to a faction)
        foreach (var fa in await db.AgentActivityLinks
            .Where(l => l.TargetType == nameof(Faction))
            .Join(db.AgentActivities.Where(a => a.ActivityDate >= sourceUtc && a.ActivityDate <= untilUtc),
                l => l.AgentActivityId, a => a.Id, (l, a) => new { a.Id, a.Title, a.Kind, a.ActivityDate, FactionId = l.TargetId })
            .Join(db.Factions.Where(f => mayClassified || !f.IsClassified),
                x => x.FactionId, f => f.Id, (x, f) => x)
            .OrderBy(x => x.ActivityDate).Take(PerSourceMax)
            .ToListAsync(ct))
        {
            var title = string.IsNullOrWhiteSpace(fa.Kind) ? fa.Title : $"{fa.Title} ({fa.Kind})";
            entries.Add(new CalendarEntry($"fa:{fa.Id}:{fa.FactionId}", title, Local(fa.ActivityDate), null,
                false, CalendarSource.FactionActivity, $"/fraktionen/{fa.FactionId}", false,
                nameof(Faction), fa.FactionId));
        }

        // meetings
        foreach (var m in await db.Meetings
            .Where(m => m.Start <= untilUtc && (m.End ?? m.Start) >= sourceUtc)
            .OrderBy(m => m.Start).Take(PerSourceMax)
            .Select(m => new { m.Id, m.Title, m.Start, m.End, m.Status })
            .ToListAsync(ct))
        {
            entries.Add(new CalendarEntry($"bes:{m.Id}", m.Title, Local(m.Start), LocalOpt(m.End),
                false, CalendarSource.Meeting, $"/besprechungen/{m.Id}",
                MeetingStatusDisplay.IsObsolete(m.Status),
                nameof(Data.Entities.Meetings.Meeting), m.Id));
        }

        // other agents' absences: never the free-text reason, and RosterVisible hides invisible agents
        var fromDay = MeetingTime.Day(sourceUtc);
        var untilDay = MeetingTime.Day(untilUtc);
        foreach (var ab in await db.Absences.RosterVisible(db)
            .Where(ab => ab.AgentId != meId && ab.FromDate <= untilDay && ab.ToDate >= fromDay)
            .OrderBy(ab => ab.FromDate).Take(PerSourceMax)
            .Select(ab => new { ab.Id, ab.FromDate, ab.ToDate, ab.Category, Codename = ab.Agent!.Codename })
            .ToListAsync(ct))
        {
            entries.Add(new CalendarEntry($"tabm:{ab.Id}", $"{ab.Codename}: {AbsenceCategoryDisplay.Name(ab.Category)}",
                ab.FromDate.ToDateTime(TimeOnly.MinValue), ab.ToDate.ToDateTime(TimeOnly.MinValue),
                true, CalendarSource.TeamAbsence, "/abmeldungen"));
        }
    }

    // utc to local
    private static DateTime Local(DateTime utc)
        => DateTime.SpecifyKind(DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime(), DateTimeKind.Unspecified);

    private static DateTime? LocalOpt(DateTime? utc) => utc is { } u ? Local(u) : null;

    private static string Truncate(string text, int max = 40)
        => text.Length <= max ? text : string.Concat(text.AsSpan(0, max), "…");
}
