using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Meetings;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services.Search.Providers;

/// <summary>Library documents: title, category and the rich-text body.</summary>
/// <remarks>The body is matched as stored HTML, so a phrase split by inline markup will not hit — the same
/// limitation the activity body has. The snippet is a window around the match, never the head of the document.</remarks>
public sealed class DocumentSearchProvider(IDbContextFactory<AppDbContext> dbFactory) : ISearchProvider
{
    public string Category => nameof(Document);

    public PartnerAccess Partner => PartnerAccess.ViaShare;

    public bool AppliesTo(SearchViewer viewer) => true;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var q = Visible(db, query);
        if (query.HasText)
        {
            var s = query.Text;
            q = q.Where(d => d.Title.Contains(s)
                || (d.Category != null && d.Category.Contains(s))
                || d.ContentHtml.Contains(s));
        }
        var rows = await q
            .OrderByDescending(d => d.Pinned)
            .ThenByDescending(d => d.ModifiedAt ?? d.CreatedAt)
            .Take(query.PerCategory)
            .Select(d => new { d.Id, d.Title, d.Category, d.ContentHtml })
            .ToListAsync(cancellationToken);

        return rows.Select(d => new SearchHit(nameof(Document), d.Id, d.Title,
                Snippet(d.Category, d.ContentHtml, query.Text), string.Empty))
            .ToList();
    }

    public async Task<IReadOnlyList<QuickHit>> QuickAsync(SearchQuery query, int max, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var s = query.Text;
        // identifiers only: the palette fires while the agent is still typing
        return await Visible(db, query).Where(d => d.Title.Contains(s))
            .OrderBy(d => d.Title).Take(max)
            .Select(d => new QuickHit(nameof(Document), d.Id, d.Title, d.Category ?? string.Empty))
            .ToListAsync(cancellationToken);
    }

    private static IQueryable<Document> Visible(AppDbContext db, SearchQuery query)
    {
        var scope = query.Scope;
        return scope.PartnerAgency is { } agency
            ? db.Documents.OnlyPartnerVisible(db, agency, scope.MeId)
            : db.Documents.OnlyVisible(db, scope.AsDocumentScope());
    }

    // category first so a category match is visibly the reason for the hit, then the text around the match
    private static string Snippet(string? category, string html, string text)
    {
        var around = SearchSnippet.Around(HtmlCleanup.PlainText(html), text);
        return string.IsNullOrWhiteSpace(category) ? around : $"{category} · {around}";
    }
}

/// <summary>Meetings. The minutes are agenda-grade: they are only matched for meetings whose agenda is open.</summary>
public sealed class MeetingSearchProvider(IDbContextFactory<AppDbContext> dbFactory) : ISearchProvider
{
    public string Category => nameof(Meeting);

    public PartnerAccess Partner => PartnerAccess.Never;

    public bool AppliesTo(SearchViewer viewer) => !viewer.IsPartner;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var q = db.Meetings.AsQueryable();
        if (query.HasText)
        {
            var s = query.Text;
            // the identifying fields are open to every internal agent; the minutes are not, and are filtered below
            q = q.Where(m => m.Title.Contains(s) || m.CaseNumber.Contains(s)
                || (m.Location != null && m.Location.Contains(s))
                || (m.MinutesHtml != null && m.MinutesHtml.Contains(s)));
        }
        var rows = await q.OrderByDescending(m => m.Start).Take(query.PerCategory)
            .Select(m => new { m.Id, m.Title, m.CaseNumber, m.Location, m.MinutesHtml, m.Start })
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return [];
        }

        // a match that exists only in the minutes must not surface before the agenda opens
        var open = await MeetingVisibility.OpenIdsAsync(
            db, rows.Select(m => m.Id).ToList(), query.Scope, query.Viewer.NowUtc, cancellationToken);
        var hits = new List<SearchHit>(rows.Count);
        foreach (var m in rows)
        {
            var identifying = !query.HasText
                || m.Title.Contains(query.Text, StringComparison.CurrentCultureIgnoreCase)
                || m.CaseNumber.Contains(query.Text, StringComparison.CurrentCultureIgnoreCase)
                || (m.Location?.Contains(query.Text, StringComparison.CurrentCultureIgnoreCase) ?? false);
            var mayReadMinutes = open.Contains(m.Id);
            if (!identifying && !mayReadMinutes)
            {
                continue;
            }
            var snippet = mayReadMinutes
                ? SearchSnippet.Around(HtmlCleanup.PlainText(m.MinutesHtml), query.Text)
                : m.Location ?? string.Empty;
            hits.Add(new SearchHit(nameof(Meeting), m.Id, m.Title, snippet, m.CaseNumber) { Timestamp = m.Start });
        }
        return hits;
    }

    public async Task<IReadOnlyList<QuickHit>> QuickAsync(SearchQuery query, int max, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var s = query.Text;
        return await db.Meetings.Where(m => m.Title.Contains(s) || m.CaseNumber.Contains(s))
            .OrderByDescending(m => m.Start).Take(max)
            .Select(m => new QuickHit(nameof(Meeting), m.Id, m.Title, m.CaseNumber))
            .ToListAsync(cancellationToken);
    }
}

/// <summary>Agenda items and their notes. Both sit behind the meeting's time gate.</summary>
public sealed class MeetingAgendaItemSearchProvider(IDbContextFactory<AppDbContext> dbFactory) : ISearchProvider
{
    public string Category => nameof(MeetingAgendaItem);

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
        var raw = await db.MeetingAgendaItems
            .Where(p => p.Title.Contains(s) || (p.NotesHtml != null && p.NotesHtml.Contains(s)))
            .OrderBy(p => p.Sorting)
            .Select(p => new { p.Id, p.MeetingId, p.Title, p.NotesHtml })
            .Take(query.PerCategory * 4)
            .ToListAsync(cancellationToken);
        if (raw.Count == 0)
        {
            return [];
        }

        var meetingIds = raw.Select(p => p.MeetingId).Distinct().ToList();
        var open = await MeetingVisibility.OpenIdsAsync(db, meetingIds, query.Scope, query.Viewer.NowUtc, cancellationToken);
        if (open.Count == 0)
        {
            return [];
        }
        var meetings = (await db.Meetings.Where(m => open.Contains(m.Id))
                .Select(m => new { m.Id, m.Title, m.CaseNumber }).ToListAsync(cancellationToken))
            .ToDictionary(m => m.Id, StringComparer.Ordinal);

        var hits = new List<SearchHit>();
        foreach (var item in raw)
        {
            if (!meetings.TryGetValue(item.MeetingId, out var meeting))
            {
                continue;
            }
            var snippet = string.IsNullOrWhiteSpace(item.NotesHtml)
                ? item.Title
                : $"{item.Title} · {SearchSnippet.Around(HtmlCleanup.PlainText(item.NotesHtml), s)}";
            hits.Add(new SearchHit(nameof(MeetingAgendaItem), meeting.Id, meeting.Title, snippet,
                meeting.CaseNumber, nameof(Meeting)));
            if (hits.Count >= query.PerCategory)
            {
                break;
            }
        }
        return hits;
    }
}

/// <summary>Library files. Title and category only — the stored file itself is never read.</summary>
public sealed class LibraryFileSearchProvider(IDbContextFactory<AppDbContext> dbFactory) : ISearchProvider
{
    public string Category => nameof(LibraryFile);

    public PartnerAccess Partner => PartnerAccess.Never;

    public bool AppliesTo(SearchViewer viewer) => !viewer.IsPartner;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var q = db.LibraryFiles.OnlyVisible(query.Scope.AsDocumentScope());
        if (query.HasText)
        {
            var s = query.Text;
            q = q.Where(f => f.Title.Contains(s)
                || (f.Category != null && f.Category.Contains(s))
                || f.OriginalName.Contains(s));
        }
        var rows = await q.OrderByDescending(f => f.ModifiedAt ?? f.CreatedAt).Take(query.PerCategory)
            .Select(f => new { f.Id, f.Title, f.Category, f.OriginalName }).ToListAsync(cancellationToken);
        return rows.Select(f => new SearchHit(nameof(LibraryFile), f.Id, f.Title,
                string.Join(" · ", new[] { f.Category, f.OriginalName }.Where(p => !string.IsNullOrWhiteSpace(p))),
                string.Empty)
            {
                // the library has no per-file page; the download endpoint is not a place to navigate to
                Href = "/dokumente",
            })
            .ToList();
    }
}
