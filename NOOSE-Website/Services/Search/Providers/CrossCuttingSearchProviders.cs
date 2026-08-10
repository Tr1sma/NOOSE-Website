using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services.Search.Providers;

/// <summary>Followups on any record. Resolved through the parent so a hit links to the right file.</summary>
public sealed class FollowupSearchProvider(IDbContextFactory<AppDbContext> dbFactory) : ISearchProvider
{
    public string Category => nameof(Followup);

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
        var raw = await db.Followups
            .Where(w => w.Note != null && w.Note.Contains(s))
            .OrderBy(w => w.DueAt)
            .Select(w => new { w.Id, w.EntityType, w.EntityId, w.Note, w.DueAt })
            .Take(query.PerCategory * 4)
            .ToListAsync(cancellationToken);

        return await SearchChildResolver.ResolveAsync(db, query, nameof(Followup),
            raw.Select(w => (w.Id, w.EntityType, w.EntityId, w.Note ?? string.Empty, (DateTime?)w.DueAt)).ToList(),
            cancellationToken);
    }
}

/// <summary>Manual links between records. Both ends must be visible, or the link itself names an invisible record.</summary>
public sealed class LinkSearchProvider(IDbContextFactory<AppDbContext> dbFactory) : ISearchProvider
{
    public string Category => nameof(Link);

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
        var raw = await db.Links
            .Where(v => v.Label != null && v.Label.Contains(s))
            .OrderByDescending(v => v.CreatedAt)
            .Select(v => new { v.Id, v.SourceType, v.SourceId, v.TargetType, v.TargetId, v.Label })
            .Take(query.PerCategory * 4)
            .ToListAsync(cancellationToken);
        if (raw.Count == 0)
        {
            return [];
        }

        // both ends, in one resolve: a link whose target is invisible would name it in the snippet
        var refs = raw.Select(v => (v.SourceType, v.SourceId))
            .Concat(raw.Select(v => (v.TargetType, v.TargetId)))
            .Distinct().ToList();
        var parents = await SearchParentResolver.ResolveVisibleAsync(db, refs, query.Viewer,
            query.HasTags ? query.TagIds : null, cancellationToken);

        var hits = new List<SearchHit>();
        foreach (var v in raw)
        {
            if (!parents.TryGetValue((v.SourceType, v.SourceId), out var source)
                || !parents.TryGetValue((v.TargetType, v.TargetId), out var target))
            {
                continue;
            }
            hits.Add(new SearchHit(nameof(Link), source.Id, source.Title,
                $"{v.Label} → {target.Title}", source.CaseNumber, source.Type));
            if (hits.Count >= query.PerCategory)
            {
                break;
            }
        }
        return hits;
    }
}

/// <summary>Custom field values on any record.</summary>
public sealed class CustomFieldValueSearchProvider(IDbContextFactory<AppDbContext> dbFactory) : ISearchProvider
{
    public string Category => nameof(CustomFieldValue);

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
        var raw = await (
            from value in db.CustomFieldValues
            where value.Value != null && value.Value.Contains(s)
            join definition in db.CustomFieldDefinitions on value.CustomFieldDefinitionId equals definition.Id
            orderby value.ModifiedAt ?? value.CreatedAt descending
            select new { value.Id, value.EntityType, value.EntityId, value.Value, Label = definition.Name })
            .Take(query.PerCategory * 4)
            .ToListAsync(cancellationToken);

        return await SearchChildResolver.ResolveAsync(db, query, nameof(CustomFieldValue),
            raw.Select(v => (v.Id, v.EntityType, v.EntityId, $"{v.Label}: {v.Value}", (DateTime?)null)).ToList(),
            cancellationToken);
    }
}

/// <summary>Shared tail of the polymorphic child providers: resolve the parent, apply the partner child release.</summary>
internal static class SearchChildResolver
{
    internal static async Task<IReadOnlyList<SearchHit>> ResolveAsync(
        AppDbContext db, SearchQuery query, string category,
        IReadOnlyList<(string ChildId, string EntityType, string EntityId, string Snippet, DateTime? When)> raw,
        CancellationToken cancellationToken)
    {
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
            released = await PartnerVisibility.VisibleChildIdsAsync(db, category,
                raw.Where(r => parents.ContainsKey((r.EntityType, r.EntityId)))
                    .Select(r => (r.EntityType, r.EntityId, r.ChildId)).ToList(),
                agency, query.Scope.MeId, cancellationToken);
        }

        var hits = new List<SearchHit>();
        foreach (var r in raw)
        {
            if (!parents.TryGetValue((r.EntityType, r.EntityId), out var parent))
            {
                continue; // parent invisible, trashed, or of a type the resolver does not know
            }
            if (released is not null && !released.Contains(r.ChildId))
            {
                continue;
            }
            hits.Add(new SearchHit(category, parent.Id, parent.Title, r.Snippet, parent.CaseNumber, parent.Type)
            {
                Timestamp = r.When,
            });
            if (hits.Count >= query.PerCategory)
            {
                break;
            }
        }
        return hits;
    }
}
