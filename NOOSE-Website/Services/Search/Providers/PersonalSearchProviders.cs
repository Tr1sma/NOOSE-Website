using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Llm;
using NOOSE_Website.Data.Entities.Notifications;
using NOOSE_Website.Data.Entities.Watchlist;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services.Search.Providers;

/// <summary>The viewer's own NOOSEI conversations, by title.</summary>
/// <remarks>
/// Titles only. The messages themselves are deliberately out: a stored tool answer can be stale with respect to
/// the owner's current scope (that is what the conversation's rights stamp exists for), so replaying one through
/// the search would hand back something the owner may no longer see. There is no AI-owner arm either — reading a
/// specific conversation on a concrete suspicion is a targeted power, and a dragnet is not.
/// </remarks>
public sealed class NooseiConversationSearchProvider(IDbContextFactory<AppDbContext> dbFactory) : ISearchProvider
{
    public string Category => nameof(NooseiConversation);

    public PartnerAccess Partner => PartnerAccess.Never;

    public bool AppliesTo(SearchViewer viewer) => !viewer.IsPartner && viewer.MeId is { Length: > 0 };

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var meId = query.Scope.MeId;
        var q = db.NooseiConversations.Where(u => u.AgentId == meId);
        if (query.HasText)
        {
            var s = query.Text;
            q = q.Where(u => u.Title.Contains(s));
        }
        return await q.OrderByDescending(u => u.LastMessageAt).Take(query.PerCategory)
            .Select(u => new SearchHit(nameof(NooseiConversation), u.Id, u.Title, string.Empty, string.Empty)
            {
                Timestamp = u.LastMessageAt,
                Href = "/ki-assistent",
            })
            .ToListAsync(cancellationToken);
    }
}

/// <summary>The viewer's own notifications.</summary>
public sealed class NotificationSearchProvider(IDbContextFactory<AppDbContext> dbFactory) : ISearchProvider
{
    public string Category => nameof(Notification);

    public PartnerAccess Partner => PartnerAccess.Never;

    public bool AppliesTo(SearchViewer viewer) => viewer.MeId is { Length: > 0 };

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var meId = query.Scope.MeId;
        var q = db.Notifications.Where(n => n.RecipientId == meId);
        if (query.HasText)
        {
            var s = query.Text;
            q = q.Where(n => n.Title.Contains(s));
        }
        return await q.OrderByDescending(n => n.CreatedAt).Take(query.PerCategory)
            // the stored Href was produced when the notification was written, by a path that had already decided
            // the recipient may follow it
            .Select(n => new SearchHit(nameof(Notification), n.Id, n.Title, string.Empty, string.Empty)
            {
                Timestamp = n.CreatedAt,
                Href = n.Href,
            })
            .ToListAsync(cancellationToken);
    }
}

/// <summary>The viewer's own saved searches, by name. The stored criteria are not a match field.</summary>
public sealed class SavedSearchSearchProvider(IDbContextFactory<AppDbContext> dbFactory) : ISearchProvider
{
    public string Category => nameof(SavedSearch);

    public PartnerAccess Partner => PartnerAccess.Never;

    public bool AppliesTo(SearchViewer viewer) => viewer.MeId is { Length: > 0 };

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var meId = query.Scope.MeId;
        var q = db.SavedSearch.Where(g => g.AgentId == meId);
        if (query.HasText)
        {
            var s = query.Text;
            q = q.Where(g => g.Name.Contains(s));
        }
        return await q.OrderBy(g => g.Name).Take(query.PerCategory)
            .Select(g => new SearchHit(nameof(SavedSearch), g.Id, g.Name, string.Empty, string.Empty)
            {
                Href = "/suche",
            })
            .ToListAsync(cancellationToken);
    }
}

/// <summary>The viewer's own graph layouts, by name. The layout JSON holds record ids and is not a match field.</summary>
public sealed class GraphCanvasLayoutSearchProvider(IDbContextFactory<AppDbContext> dbFactory) : ISearchProvider
{
    public string Category => nameof(GraphCanvasLayout);

    public PartnerAccess Partner => PartnerAccess.Never;

    public bool AppliesTo(SearchViewer viewer) => viewer.MeId is { Length: > 0 };

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var meId = query.Scope.MeId;
        var q = db.GraphCanvasLayouts.Where(l => l.AgentId == meId);
        if (query.HasText)
        {
            var s = query.Text;
            q = q.Where(l => l.Name.Contains(s));
        }
        return await q.OrderBy(l => l.Name).Take(query.PerCategory)
            .Select(l => new SearchHit(nameof(GraphCanvasLayout), l.Id, l.Name, string.Empty, string.Empty)
            {
                Href = "/graph",
            })
            .ToListAsync(cancellationToken);
    }
}

/// <summary>The viewer's own watchlist. Only the resolved record title, and only while it stays resolvable.</summary>
/// <remarks>An entry whose record the viewer may no longer see is dropped entirely rather than shown as
/// "(nicht mehr zugänglich)" — on the watchlist page that label is fine, but in a keyword search it would confirm
/// a match against a record they have lost access to.</remarks>
public sealed class WatchlistEntrySearchProvider(IDbContextFactory<AppDbContext> dbFactory) : ISearchProvider
{
    public string Category => nameof(WatchlistEntry);

    public PartnerAccess Partner => PartnerAccess.Never;

    public bool AppliesTo(SearchViewer viewer) => viewer.MeId is { Length: > 0 };

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var meId = query.Scope.MeId;
        var raw = await db.Watchlists.Where(w => w.AgentId == meId)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new { w.Id, w.EntityType, w.EntityId, w.CreatedAt })
            .Take(query.PerCategory * 4)
            .ToListAsync(cancellationToken);
        if (raw.Count == 0)
        {
            return [];
        }

        var parents = await SearchParentResolver.ResolveVisibleAsync(db,
            raw.Select(w => (w.EntityType, w.EntityId)).Distinct().ToList(), query.Viewer,
            query.HasTags ? query.TagIds : null, cancellationToken);

        var hits = new List<SearchHit>();
        foreach (var w in raw)
        {
            if (!parents.TryGetValue((w.EntityType, w.EntityId), out var record))
            {
                continue;
            }
            // matched on the resolved title, because that is the only text this row has
            if (query.HasText && !record.Title.Contains(query.Text, StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }
            hits.Add(new SearchHit(nameof(WatchlistEntry), record.Id, record.Title,
                SearchCatalog.German(record.Type), record.CaseNumber, record.Type)
            {
                Timestamp = w.CreatedAt,
            });
            if (hits.Count >= query.PerCategory)
            {
                break;
            }
        }
        return hits;
    }
}
