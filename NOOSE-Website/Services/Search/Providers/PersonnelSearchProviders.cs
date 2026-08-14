using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Absences;
using NOOSE_Website.Data.Entities.Announcements;
using NOOSE_Website.Data.Entities.Appointments;
using NOOSE_Website.Data.Entities.Feedback;
using NOOSE_Website.Data.Entities.Informants;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Personnel;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Models.Common;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services.Search.Providers;

/// <summary>Personnel files. Leadership only, and the real name is a separate right on top of that.</summary>
public sealed class AgentSearchProvider(IDbContextFactory<AppDbContext> dbFactory) : ISearchProvider
{
    public string Category => nameof(Agent);

    public PartnerAccess Partner => PartnerAccess.Never;

    public bool AppliesTo(SearchViewer viewer) => viewer.Scope.MayClassifiedRead;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var mayRealName = query.Viewer.User.MayRealNameSee();
        // the roster rule of /personal, named once: raw db.Users would hand out team leads (RP-invisible),
        // blocked accounts and applicants — exactly what the page hides
        var q = db.Users.OnlyWithPersonnelFile();
        if (query.HasText)
        {
            var s = query.Text;
            // the real name is only a match field for viewers allowed to read it; otherwise a hit on it would
            // confirm the name to someone who may not see it
            q = mayRealName
                ? q.Where(u => (u.Codename != null && u.Codename.Contains(s))
                    || (u.RealName != null && u.RealName.Contains(s))
                    || (u.BadgeNumber != null && u.BadgeNumber.Contains(s)))
                : q.Where(u => (u.Codename != null && u.Codename.Contains(s))
                    || (u.BadgeNumber != null && u.BadgeNumber.Contains(s)));
        }
        var rows = await q.OrderBy(u => u.Codename).Take(query.PerCategory)
            .Select(u => new { u.Id, u.Codename, u.RealName, u.BadgeNumber, u.Rank })
            .ToListAsync(cancellationToken);
        return rows.Select(u => new SearchHit(nameof(Agent), u.Id,
                AgentNameDisplay.Pick(u.Codename, u.RealName, mayRealName),
                RankDisplay.Name(u.Rank), u.BadgeNumber ?? string.Empty))
            .ToList();
    }

    public async Task<IReadOnlyList<QuickHit>> QuickAsync(SearchQuery query, int max, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var s = query.Text;
        var rows = await db.Users.OnlyWithPersonnelFile()
            .Where(u => u.Codename != null && u.Codename.Contains(s))
            .OrderBy(u => u.Codename).Take(max)
            .Select(u => new { u.Id, u.Codename, u.BadgeNumber }).ToListAsync(cancellationToken);
        return rows
            .Select(u => new QuickHit(nameof(Agent), u.Id, u.Codename ?? AgentNameDisplay.Unnamed, u.BadgeNumber ?? string.Empty))
            .ToList();
    }

    public async Task<IReadOnlyList<SearchHit>> ResolveIdsAsync(
        SearchQuery query, IReadOnlyCollection<string> ids, int take, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // the index only ever carries codenames, but the display still goes through the real-name gate
        var mayRealName = query.Viewer.User.MayRealNameSee();
        var rows = await db.Users.OnlyWithPersonnelFile().Where(u => ids.Contains(u.Id)).Take(take)
            .Select(u => new { u.Id, u.Codename, u.RealName, u.BadgeNumber, u.Rank })
            .ToListAsync(cancellationToken);
        return rows.Select(u => new SearchHit(nameof(Agent), u.Id,
                AgentNameDisplay.Pick(u.Codename, u.RealName, mayRealName),
                RankDisplay.Name(u.Rank), u.BadgeNumber ?? string.Empty))
            .ToList();
    }
}

/// <summary>Personnel notes. Same gate as the file they sit in; the subject appears as a codename.</summary>
public sealed class AgentNoteSearchProvider(IDbContextFactory<AppDbContext> dbFactory) : ISearchProvider
{
    public string Category => nameof(AgentNote);

    public PartnerAccess Partner => PartnerAccess.Never;

    public bool AppliesTo(SearchViewer viewer) => viewer.Scope.MayClassifiedRead;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
    {
        if (!query.HasText)
        {
            return [];
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var s = query.Text;
        var mayRealName = query.Viewer.User.MayRealNameSee();
        var rows = await (
            from n in db.AgentNotes
            where n.Text.Contains(s) || (n.ArtFreetext != null && n.ArtFreetext.Contains(s))
            // same roster rule: a note on a team lead's file would name an account that is invisible RP-wide
            join u in db.Users.OnlyWithPersonnelFile() on n.AgentId equals u.Id
            orderby n.EntryDate descending
            select new { n.Id, n.AgentId, n.Text, n.ArtFreetext, n.EntryDate, u.Codename, u.RealName })
            .Take(query.PerCategory)
            .ToListAsync(cancellationToken);

        return rows.Select(n => new SearchHit(nameof(AgentNote), n.AgentId,
                AgentNameDisplay.Pick(n.Codename, n.RealName, mayRealName),
                string.IsNullOrWhiteSpace(n.ArtFreetext)
                    ? HtmlCleanup.PlainText(n.Text)
                    : $"{n.ArtFreetext} · {HtmlCleanup.PlainText(n.Text)}",
                string.Empty, nameof(Agent))
            {
                Timestamp = n.EntryDate,
            })
            .ToList();
    }
}

/// <summary>Confidential informants. Open to every internal agent, never to a partner.</summary>
public sealed class InformantSearchProvider(IDbContextFactory<AppDbContext> dbFactory) : ISearchProvider
{
    public string Category => nameof(Informant);

    public PartnerAccess Partner => PartnerAccess.Never;

    public bool AppliesTo(SearchViewer viewer) => !viewer.IsPartner;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var q = db.Informants.AsQueryable();
        if (query.HasText)
        {
            var s = query.Text;
            q = q.Where(i => i.CaseNumber.Contains(s)
                || (i.RealName != null && i.RealName.Contains(s))
                || (i.Description != null && i.Description.Contains(s))
                || (i.ContactInfo != null && i.ContactInfo.Contains(s))
                || (i.Notes != null && i.Notes.Contains(s)));
        }
        // record access is all-or-nothing here, so the full detail may be shown once the viewer passed the gate
        return await q.OrderBy(i => i.CaseNumber).Take(query.PerCategory)
            .Select(i => new SearchHit(nameof(Informant), i.Id, i.RealName ?? i.CaseNumber,
                i.Description ?? string.Empty, i.CaseNumber))
            .ToListAsync(cancellationToken);
    }
}

/// <summary>Informant meeting reports. Behind the same all-or-nothing gate as the informant.</summary>
public sealed class InformantMeetingSearchProvider(IDbContextFactory<AppDbContext> dbFactory) : ISearchProvider
{
    public string Category => nameof(InformantMeeting);

    public PartnerAccess Partner => PartnerAccess.Never;

    public bool AppliesTo(SearchViewer viewer) => !viewer.IsPartner;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
    {
        if (!query.HasText)
        {
            return [];
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var s = query.Text;
        var rows = await (
            from m in db.InformantMeetings
            where (m.Content != null && m.Content.Contains(s)) || (m.Location != null && m.Location.Contains(s))
            join i in db.Informants on m.InformantId equals i.Id
            orderby m.MeetingDate descending
            select new { m.Id, m.InformantId, m.Content, m.MeetingDate, i.RealName, i.CaseNumber })
            .Take(query.PerCategory)
            .ToListAsync(cancellationToken);

        return rows.Select(m => new SearchHit(nameof(InformantMeeting), m.InformantId,
                m.RealName ?? m.CaseNumber, SearchSnippet.Around(HtmlCleanup.PlainText(m.Content), s),
                m.CaseNumber, nameof(Informant))
            {
                Timestamp = m.MeetingDate,
            })
            .ToList();
    }
}

/// <summary>Observations. Anchored on the person they were made about.</summary>
public sealed class ObservationSearchProvider(IDbContextFactory<AppDbContext> dbFactory) : ISearchProvider
{
    public string Category => nameof(Observation);

    public PartnerAccess Partner => PartnerAccess.ViaParentShare;

    public bool AppliesTo(SearchViewer viewer) => true;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
    {
        if (!query.HasText)
        {
            return [];
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var s = query.Text;
        var raw = await db.Observations
            .Where(o => (o.Sighting != null && o.Sighting.Contains(s))
                || (o.Result != null && o.Result.Contains(s))
                || (o.Location != null && o.Location.Contains(s)))
            .OrderByDescending(o => o.Start)
            .Select(o => new { o.Id, o.PersonId, o.Sighting, o.Result, o.Start })
            .Take(query.PerCategory * 4)
            .ToListAsync(cancellationToken);
        if (raw.Count == 0)
        {
            return [];
        }

        var parents = await SearchParentResolver.ResolveVisibleAsync(db,
            raw.Select(o => (nameof(Person), o.PersonId)).Distinct().ToList(), query.Viewer,
            query.HasTags ? query.TagIds : null, cancellationToken);

        HashSet<string>? released = null;
        if (query.Scope.PartnerAgency is { } agency)
        {
            released = await PartnerVisibility.VisibleChildIdsAsync(db, nameof(Observation),
                raw.Where(o => parents.ContainsKey((nameof(Person), o.PersonId)))
                    .Select(o => (nameof(Person), o.PersonId, o.Id)).ToList(),
                agency, query.Scope.MeId, cancellationToken);
        }

        var hits = new List<SearchHit>();
        foreach (var o in raw)
        {
            if (!parents.TryGetValue((nameof(Person), o.PersonId), out var person))
            {
                continue;
            }
            if (released is not null && !released.Contains(o.Id))
            {
                continue;
            }
            hits.Add(new SearchHit(nameof(Observation), person.Id, person.Title,
                (o.Sighting ?? o.Result) ?? string.Empty, person.CaseNumber, nameof(Person))
            {
                Timestamp = o.Start,
            });
            if (hits.Count >= query.PerCategory)
            {
                break;
            }
        }
        return hits;
    }
}

/// <summary>Taskforce chat. Members only, and partners on a released taskforce.</summary>
public sealed class TaskforceMessageSearchProvider(IDbContextFactory<AppDbContext> dbFactory) : ISearchProvider
{
    public string Category => nameof(TaskforceMessage);

    public PartnerAccess Partner => PartnerAccess.ViaParentShare;

    public bool AppliesTo(SearchViewer viewer) => true;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
    {
        if (!query.HasText)
        {
            return [];
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var s = query.Text;
        var raw = await db.TaskforceMessages
            .Where(m => m.Text.Contains(s))
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new { m.Id, m.TaskforceId, m.Text, m.AuthorName, m.CreatedAt })
            .Take(query.PerCategory * 4)
            .ToListAsync(cancellationToken);
        if (raw.Count == 0)
        {
            return [];
        }

        var parents = await SearchParentResolver.ResolveVisibleAsync(db,
            raw.Select(m => (nameof(Taskforce), m.TaskforceId)).Distinct().ToList(), query.Viewer,
            query.HasTags ? query.TagIds : null, cancellationToken);

        var hits = new List<SearchHit>();
        foreach (var m in raw)
        {
            if (!parents.TryGetValue((nameof(Taskforce), m.TaskforceId), out var taskforce))
            {
                continue;
            }
            hits.Add(new SearchHit(nameof(TaskforceMessage), taskforce.Id, taskforce.Title,
                HtmlCleanup.PlainText(m.Text), taskforce.CaseNumber, nameof(Taskforce))
            {
                // stored codename; a real name never reaches a chat line
                Actor = m.AuthorName,
                Timestamp = m.CreatedAt,
            });
            if (hits.Count >= query.PerCategory)
            {
                break;
            }
        }
        return hits;
    }
}

/// <summary>Board announcements, filtered to the viewer's audience.</summary>
public sealed class AnnouncementSearchProvider(IDbContextFactory<AppDbContext> dbFactory) : ISearchProvider
{
    public string Category => nameof(Announcement);

    public PartnerAccess Partner => PartnerAccess.Never;

    public bool AppliesTo(SearchViewer viewer) => !viewer.IsPartner;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var myTaskforces = await AnnouncementVisibility.MyTaskforceIdsAsync(db, query.Scope.MeId, cancellationToken);
        var q = db.Announcements.OnlyVisible(query.Viewer.User, myTaskforces);
        if (query.HasText)
        {
            var s = query.Text;
            q = q.Where(a => a.Title.Contains(s) || a.CaseNumber.Contains(s) || a.Content.Contains(s));
        }
        var rows = await q.OrderByDescending(a => a.Important).ThenByDescending(a => a.CreatedAt)
            .Take(query.PerCategory)
            .Select(a => new { a.Id, a.Title, a.CaseNumber, a.Content, a.CreatedAt })
            .ToListAsync(cancellationToken);
        return rows.Select(a => new SearchHit(nameof(Announcement), a.Id, a.Title,
                SearchSnippet.Around(HtmlCleanup.PlainText(a.Content), query.Text), a.CaseNumber)
            {
                Timestamp = a.CreatedAt,
            })
            .ToList();
    }
}

/// <summary>Calendar appointments; a restricted one only for its creator and assignees.</summary>
public sealed class AppointmentSearchProvider(IDbContextFactory<AppDbContext> dbFactory) : ISearchProvider
{
    public string Category => nameof(Appointment);

    public PartnerAccess Partner => PartnerAccess.Never;

    public bool AppliesTo(SearchViewer viewer) => !viewer.IsPartner;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var scope = query.Scope;
        var q = db.Appointments.OnlyVisible(db, scope.MayAllTaskforces, scope.MeId);
        if (query.HasText)
        {
            var s = query.Text;
            q = q.Where(t => t.Title.Contains(s) || t.CaseNumber.Contains(s)
                || (t.Location != null && t.Location.Contains(s))
                || (t.Description != null && t.Description.Contains(s)));
        }
        return await q.OrderByDescending(t => t.Start).Take(query.PerCategory)
            .Select(t => new SearchHit(nameof(Appointment), t.Id, t.Title,
                t.Location ?? t.Description ?? string.Empty, t.CaseNumber) { Timestamp = t.Start })
            .ToListAsync(cancellationToken);
    }
}

/// <summary>Absences. The roster tier sees who is away, never why.</summary>
public sealed class AbsenceSearchProvider(IDbContextFactory<AppDbContext> dbFactory) : ISearchProvider
{
    public string Category => nameof(Absence);

    public PartnerAccess Partner => PartnerAccess.Never;

    public bool AppliesTo(SearchViewer viewer) => !viewer.IsPartner && viewer.MeId is { Length: > 0 };

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var meId = query.Scope.MeId;
        // ask for everything and let the canonical helper decide what this viewer actually gets
        var granted = AbsenceVisibility.Granted(query.Viewer.User, AbsenceViewScope.All);
        var q = db.Absences.OnlyVisible(db, granted, meId);
        if (query.HasText)
        {
            var s = query.Text;
            // the reason is only a match field where it may also be read, or a hit on it confirms free text
            // the roster tier is never shown
            q = granted == AbsenceViewScope.All
                ? q.Where(a => (a.Reason != null && a.Reason.Contains(s)) || a.Agent!.Codename!.Contains(s))
                : q.Where(a => (a.AgentId == meId && a.Reason != null && a.Reason.Contains(s))
                    || a.Agent!.Codename!.Contains(s));
        }
        var rows = await q.OrderByDescending(a => a.FromDate).Take(query.PerCategory)
            .Select(a => new { a.Id, a.AgentId, a.FromDate, a.ToDate, a.Reason, Codename = a.Agent!.Codename })
            .ToListAsync(cancellationToken);

        return rows.Select(a => new SearchHit(nameof(Absence), a.Id,
                $"{a.Codename} · {a.FromDate:dd.MM.yyyy}–{a.ToDate:dd.MM.yyyy}",
                AbsenceVisibility.MayReadPrivateFields(granted, a.AgentId == meId) ? a.Reason ?? string.Empty : string.Empty,
                string.Empty)
            {
                Href = granted == AbsenceViewScope.All ? "/abmeldungen?tab=uebersicht" : "/abmeldungen?tab=meine",
            })
            .ToList();
    }
}

/// <summary>Feedback reports: own always, everything for leadership.</summary>
public sealed class FeedbackSearchProvider(IDbContextFactory<AppDbContext> dbFactory) : ISearchProvider
{
    public string Category => nameof(Feedback);

    public PartnerAccess Partner => PartnerAccess.Never;

    public bool AppliesTo(SearchViewer viewer) => !viewer.IsPartner && viewer.MeId is { Length: > 0 };

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var meId = query.Scope.MeId;
        var all = query.Scope.MayClassifiedRead;
        var q = all ? db.Feedbacks.AsQueryable() : db.Feedbacks.Where(f => f.AgentId == meId);
        if (query.HasText)
        {
            var s = query.Text;
            q = q.Where(f => f.Text.Contains(s)
                || (f.Response != null && f.Response.Contains(s))
                || (f.PageRoute != null && f.PageRoute.Contains(s)));
        }
        var rows = await q.OrderByDescending(f => f.CreatedAt).Take(query.PerCategory)
            .Select(f => new { f.Id, f.AgentId, f.Text, f.PageRoute, f.CreatedAt }).ToListAsync(cancellationToken);
        return rows.Select(f => new SearchHit(nameof(Feedback), f.Id,
                string.IsNullOrWhiteSpace(f.PageRoute) ? "Feedback" : $"Feedback · {f.PageRoute}",
                f.Text, string.Empty)
            {
                Timestamp = f.CreatedAt,
                Href = f.AgentId == meId ? "/feedback?tab=meine" : "/feedback?tab=eingang",
            })
            .ToList();
    }
}
