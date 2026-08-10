using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services.Search;

/// <summary>Plumbing every provider needs and none should own.</summary>
/// <remarks>
/// Static, like the rest of the shared logic under <c>Services/</c>. The entity-specific part of a fuzzy pass is
/// which fields feed the token list; the dedupe, the distance sort and the cap are the same everywhere, and forty
/// copies of them would be forty chances to drift.
/// </remarks>
public static class SearchProviderKit
{
    /// <summary>Candidate for the in-memory fuzzy pass: display data plus the words to compare.</summary>
    public readonly record struct Candidate(
        string Id, string Display, string CaseNumber, string Snippet, IReadOnlyList<string> Tokens);

    /// <summary>Appends Levenshtein-similar candidates to the substring hits, deduped and sorted by distance.</summary>
    /// <remarks>Substring hits stay first: an exact match must never be pushed below a typo match.</remarks>
    public static IReadOnlyList<SearchHit> FuzzySupplement(
        string category, IReadOnlyList<SearchHit> substring, SearchQuery query, IEnumerable<Candidate> candidates)
    {
        if (query.Tokens.Count == 0)
        {
            return substring;
        }
        var exists = substring.Select(t => t.TargetId).ToHashSet(StringComparer.Ordinal);
        var fuzzy = new List<(SearchHit Hit, int Distance)>();
        foreach (var candidate in candidates)
        {
            if (exists.Contains(candidate.Id))
            {
                continue;
            }
            if (TextSimilarity.PhraseSimilar(query.Tokens, candidate.Tokens, out var distance))
            {
                fuzzy.Add((new SearchHit(category, candidate.Id, candidate.Display, candidate.Snippet, candidate.CaseNumber), distance));
            }
        }
        if (fuzzy.Count == 0)
        {
            return substring;
        }
        var result = new List<SearchHit>(substring);
        result.AddRange(fuzzy.OrderBy(f => f.Distance).Select(f => f.Hit));
        return result.Count > query.PerCategory ? result.Take(query.PerCategory).ToList() : result;
    }

    /// <summary>Lightweight fuzzy supplement for the palette: identifiers only (name/title + case number).</summary>
    public static IReadOnlyList<QuickHit> QuickFuzzy(
        string category, IReadOnlyList<QuickHit> already, SearchQuery query,
        IEnumerable<(string Id, string Name, string CaseNumber)> candidates, int max)
    {
        if (query.Tokens.Count == 0)
        {
            return already;
        }
        var exists = already.Select(t => t.TargetId).ToHashSet(StringComparer.Ordinal);
        var fuzzy = new List<(QuickHit Hit, int Distance)>();
        foreach (var candidate in candidates)
        {
            if (exists.Contains(candidate.Id))
            {
                continue;
            }
            if (TextSimilarity.PhraseSimilar(query.Tokens, TextSimilarity.Tokens(candidate.Name, candidate.CaseNumber), out var distance))
            {
                fuzzy.Add((new QuickHit(category, candidate.Id, candidate.Name, candidate.CaseNumber), distance));
            }
        }
        if (fuzzy.Count == 0)
        {
            return already;
        }
        var result = new List<QuickHit>(already);
        result.AddRange(fuzzy.OrderBy(f => f.Distance).Take(max).Select(f => f.Hit));
        return result;
    }

    /// <summary>Round-robin merge of several hit lists.</summary>
    /// <remarks>Keeps one populous category from crowding out the rest: taking the lists end to end and cutting at
    /// N returns "the first N people" whenever people match at all — in the palette and, worse, in the assistant's
    /// tool result, where it reads as the whole answer.</remarks>
    public static IEnumerable<T> Interleave<T>(IReadOnlyList<IReadOnlyList<T>> lists)
    {
        for (var index = 0; ; index++)
        {
            var some = false;
            foreach (var list in lists)
            {
                if (index < list.Count)
                {
                    some = true;
                    yield return list[index];
                }
            }
            if (!some)
            {
                yield break;
            }
        }
    }

    /// <inheritdoc cref="Interleave{T}" />
    public static IEnumerable<QuickHit> Shuffle(IReadOnlyList<IReadOnlyList<QuickHit>> lists) => Interleave(lists);
}
