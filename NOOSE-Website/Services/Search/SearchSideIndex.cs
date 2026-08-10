using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services.Search;

/// <summary>Recall from the persisted phonetic/stem side index — the pass that catches Maier↔Meyer, which
/// Levenshtein on the raw string misses.</summary>
/// <remarks>
/// Two queries cover every indexed type at once, so the lookup lives here rather than in each provider. The
/// resolution of a candidate id back to a hit does not: that goes through
/// <see cref="ISearchProvider.ResolveIdsAsync"/>, because the alternative is a second switch carrying a second
/// copy of every visibility rule — which is exactly how the old one came to ignore the partner and tag filters.
/// </remarks>
public static class SearchSideIndex
{
    /// <summary>Appends side-index hits to the groups already collected, in place.</summary>
    public static async Task AppendAsync(
        IDbContextFactory<AppDbContext> dbFactory,
        IReadOnlyList<ISearchProvider> providers,
        List<SearchResultGroup> groups,
        SearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var phonetic = SearchTokenizer.PhoneticKeys(query.Text);
        var stems = SearchTokenizer.Stems(query.Text);
        if (phonetic.Count == 0 && stems.Count == 0)
        {
            return;
        }
        var byCategory = providers
            .Where(p => SearchCatalog.Has(p.Category, SearchTraits.SideIndexed))
            .ToDictionary(p => p.Category, StringComparer.Ordinal);
        if (byCategory.Count == 0)
        {
            return;
        }
        var types = byCategory.Keys.ToHashSet(StringComparer.Ordinal);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var candidates = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        void Note(string type, string id)
        {
            if (!candidates.TryGetValue(type, out var set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                candidates[type] = set;
            }
            set.Add(id);
        }

        if (phonetic.Count > 0)
        {
            foreach (var row in await db.SearchPhoneticKeys
                         .Where(k => types.Contains(k.EntityType) && phonetic.Contains(k.Key))
                         .Select(k => new { k.EntityType, k.EntityId }).Distinct().ToListAsync(cancellationToken))
            {
                Note(row.EntityType, row.EntityId);
            }
        }
        if (stems.Count > 0)
        {
            foreach (var row in await db.SearchStemTokens
                         .Where(k => types.Contains(k.EntityType) && stems.Contains(k.Stem))
                         .Select(k => new { k.EntityType, k.EntityId }).Distinct().ToListAsync(cancellationToken))
            {
                Note(row.EntityType, row.EntityId);
            }
        }

        foreach (var (type, ids) in candidates)
        {
            var existing = groups.FirstOrDefault(g => g.Category == type);
            var have = existing is null
                ? new HashSet<string>(StringComparer.Ordinal)
                : existing.Hit.Select(h => h.TargetId).ToHashSet(StringComparer.Ordinal);
            var need = ids.Where(id => !have.Contains(id)).ToList();
            var slots = query.PerCategory - (existing?.Hit.Count ?? 0);
            if (need.Count == 0 || slots <= 0)
            {
                continue;
            }
            var extra = await byCategory[type].ResolveIdsAsync(query, need, slots, cancellationToken);
            if (extra.Count == 0)
            {
                continue;
            }
            var merged = (existing?.Hit ?? Enumerable.Empty<SearchHit>()).Concat(extra).ToList();
            if (existing is not null)
            {
                // replace in place: remove-and-append would sink a supplemented category to the tail of the page
                groups[groups.IndexOf(existing)] = existing with { Hit = merged };
            }
            else
            {
                groups.Add(new SearchResultGroup(type, SearchCatalog.Plural(type), merged));
            }
        }
    }
}
