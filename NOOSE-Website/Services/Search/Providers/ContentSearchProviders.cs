using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Models.Common;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services.Search.Providers;

/// <summary>Person dossier entries. The dok has no page of its own, so a hit targets the person's file.</summary>
public sealed class PersonDocSearchProvider(IDbContextFactory<AppDbContext> dbFactory) : ISearchProvider
{
    public string Category => nameof(PersonDoc);

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
        var deep = query.Deep;
        var tagIds = query.TagIds;
        var hasTags = query.HasTags;

        // explicit join on People rather than Include: the required navigation is soft-delete filtered
        return await (
            from d in db.PersonDocs
            where (d.Reason != null && d.Reason.Contains(s)) || (d.ReceivedInformation != null && d.ReceivedInformation.Contains(s))
                || (deep && d.Faction != null && d.Faction.Contains(s))
            join p in db.People.OnlyVisible(query.Scope) on d.PersonId equals p.Id
            where !hasTags || db.TagMappings.Any(z => z.EntityType == nameof(Person) && z.EntityId == p.Id && tagIds.Contains(z.TagId))
            orderby d.Timestamp descending
            // TargetId is the person's, so the target type must say so
            select new SearchHit(nameof(PersonDoc), p.Id, p.Name,
                (d.Reason ?? d.ReceivedInformation) ?? string.Empty, p.CaseNumber, nameof(Person)))
            .Take(query.PerCategory)
            .ToListAsync(cancellationToken);
    }
}

/// <summary>Sources attached to any record. Resolved through the parent so a hit links to the right file.</summary>
public sealed class SourceSearchProvider(IDbContextFactory<AppDbContext> dbFactory) : ISearchProvider
{
    public string Category => nameof(Source);

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
        var raw = await db.Sources
            .Where(source => source.Title.Contains(s) || (source.Description != null && source.Description.Contains(s)))
            .OrderByDescending(source => source.CreatedAt)
            .Select(source => new
            {
                source.Id, source.EntityType, source.EntityId, source.Title, source.Type, source.TargetId, source.IsInternalOnly,
            })
            .Take(query.PerCategory * 4)
            .ToListAsync(cancellationToken);
        if (raw.Count == 0)
        {
            return [];
        }

        var parents = await SearchParentResolver.ResolveVisibleAsync(db,
            raw.Select(r => (r.EntityType, r.EntityId)).Distinct().ToList(), query.Viewer,
            query.HasTags ? query.TagIds : null, cancellationToken);

        var myTaskforces = await SourceVisibility.MyTaskforceIdsAsync(db, query.Scope, cancellationToken);
        HashSet<string>? released = null;
        HashSet<string>? visibleDocuments = null;
        if (query.Scope.PartnerAgency is { } agency)
        {
            released = await PartnerVisibility.VisibleChildIdsAsync(db, nameof(Source),
                raw.Where(r => parents.ContainsKey((r.EntityType, r.EntityId)))
                    .Select(r => (r.EntityType, r.EntityId, r.Id)).ToList(),
                agency, query.Scope.MeId, cancellationToken);
            var documentIds = raw.Where(r => r.Type == SourceType.Document && r.TargetId != null)
                .Select(r => r.TargetId!).Distinct().ToList();
            visibleDocuments = documentIds.Count == 0
                ? []
                : (await db.Documents.OnlyPartnerVisible(db, agency, query.Scope.MeId)
                        .Where(d => documentIds.Contains(d.Id)).Select(d => d.Id).ToListAsync(cancellationToken))
                    .ToHashSet(StringComparer.Ordinal);
        }

        var hits = new List<SearchHit>();
        foreach (var r in raw)
        {
            if (!parents.TryGetValue((r.EntityType, r.EntityId), out var parent))
            {
                continue; // parent invisible, trashed, or of a type the resolver does not know
            }
            if (r.IsInternalOnly && !SourceVisibility.MaySee(
                    new Source { IsInternalOnly = true }, r.EntityType, r.EntityId, query.Scope, myTaskforces))
            {
                continue;
            }
            if (released is not null)
            {
                // partners: internal cross-references never, documents only when the target itself is released
                if (r.Type == SourceType.Internal || !released.Contains(r.Id))
                {
                    continue;
                }
                if (r.Type == SourceType.Document && (r.TargetId is null || visibleDocuments?.Contains(r.TargetId) != true))
                {
                    continue;
                }
            }
            hits.Add(new SearchHit(nameof(Source), parent.Id, parent.Title, r.Title, parent.CaseNumber, parent.Type));
            if (hits.Count >= query.PerCategory)
            {
                break;
            }
        }
        return hits;
    }
}

/// <summary>Comments on any record. Resolved through the parent so a hit links to the right file.</summary>
public sealed class CommentSearchProvider(IDbContextFactory<AppDbContext> dbFactory) : ISearchProvider
{
    public string Category => nameof(Comment);

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
        var raw = await db.Comments
            .Where(comment => comment.Text.Contains(s))
            .OrderByDescending(comment => comment.CreatedAt)
            .Select(comment => new { comment.Id, comment.EntityType, comment.EntityId, comment.Text })
            .Take(query.PerCategory * 4)
            .ToListAsync(cancellationToken);
        if (raw.Count == 0)
        {
            return [];
        }

        var parents = await SearchParentResolver.ResolveVisibleAsync(db,
            raw.Select(r => (r.EntityType, r.EntityId)).Distinct().ToList(), query.Viewer,
            query.HasTags ? query.TagIds : null, cancellationToken);

        HashSet<string>? released = null;
        if (query.Scope.PartnerAgency is { } agency)
        {
            released = await PartnerVisibility.VisibleChildIdsAsync(db, nameof(Comment),
                raw.Where(r => parents.ContainsKey((r.EntityType, r.EntityId)))
                    .Select(r => (r.EntityType, r.EntityId, r.Id)).ToList(),
                agency, query.Scope.MeId, cancellationToken);
        }

        var hits = new List<SearchHit>();
        foreach (var r in raw)
        {
            if (!parents.TryGetValue((r.EntityType, r.EntityId), out var parent))
            {
                continue;
            }
            if (released is not null && !released.Contains(r.Id))
            {
                continue;
            }
            hits.Add(new SearchHit(nameof(Comment), parent.Id, parent.Title, r.Text, parent.CaseNumber, parent.Type));
            if (hits.Count >= query.PerCategory)
            {
                break;
            }
        }
        return hits;
    }
}
