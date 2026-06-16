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
            await LoadAuthorityAsync(db, sourceUtc, untilUtc, mayClassified, entries, cancellationToken);
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
                t.AllDay, CalendarSource.Appointment, $"/kalender/{t.Id}", AppointmentStatusDisplay.IsObsolete(t.Status)));
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
                false, CalendarSource.Job, $"/aufgaben/{a.Id}", JobStatusDisplay.IsCompleted(a.Status)));
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
                    false, CalendarSource.Followup, mayName ? parents.Href : null));
            }
        }
    }

    // ---- authority calendar ----
    private async Task LoadAuthorityAsync(AppDbContext db, DateTime sourceUtc, DateTime untilUtc, bool mayClassified,
        List<CalendarEntry> entries, CancellationToken ct)
    {
        // public appointments
        foreach (var t in await db.Appointments.ForAuthority(mayClassified)
            .Where(t => t.Start <= untilUtc && (t.End ?? t.Start) >= sourceUtc)
            .OrderBy(t => t.Start).Take(PerSourceMax)
            .Select(t => new { t.Id, t.Title, t.Start, t.End, t.AllDay, t.Status })
            .ToListAsync(ct))
        {
            entries.Add(new CalendarEntry($"tm:{t.Id}", t.Title, Local(t.Start), LocalOpt(t.End),
                t.AllDay, CalendarSource.Appointment, $"/kalender/{t.Id}", AppointmentStatusDisplay.IsObsolete(t.Status)));
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
                false, CalendarSource.Operation, $"/operationen/{o.Id}", o.Status == OperationStatus.Aborted));
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
            entries.Add(new CalendarEntry($"ob:{ob.Id}", title, Local(ob.Start), LocalOpt(ob.End),
                false, CalendarSource.Observation, $"/personen/{ob.PersonId}"));
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
                false, CalendarSource.PersonDoc, $"/personen/{d.PersonId}?tab=doks"));
        }

        // faction activities
        foreach (var fa in await db.FactionActivities
            .Where(fa => (mayClassified || !fa.Faction!.IsClassified)
                && fa.Timestamp >= sourceUtc && fa.Timestamp <= untilUtc)
            .OrderBy(fa => fa.Timestamp).Take(PerSourceMax)
            .Select(fa => new { fa.Id, fa.Title, fa.Kind, fa.Timestamp, fa.FactionId })
            .ToListAsync(ct))
        {
            var title = string.IsNullOrWhiteSpace(fa.Kind) ? fa.Title : $"{fa.Title} ({fa.Kind})";
            entries.Add(new CalendarEntry($"fa:{fa.Id}", title, Local(fa.Timestamp), null,
                false, CalendarSource.FactionActivity, $"/fraktionen/{fa.FactionId}"));
        }
    }

    // utc to local
    private static DateTime Local(DateTime utc)
        => DateTime.SpecifyKind(DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime(), DateTimeKind.Unspecified);

    private static DateTime? LocalOpt(DateTime? utc) => utc is { } u ? Local(u) : null;

    private static string Truncate(string text, int max = 40)
        => text.Length <= max ? text : string.Concat(text.AsSpan(0, max), "…");
}
