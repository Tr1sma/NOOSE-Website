using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <summary>In-memory relevance ranking for search hits; MySQL/Pomelo can only recall via LIKE, so ordering happens here. Reuses TextSimilarity for typo tolerance.</summary>
public static class SearchRelevance
{
    // field-weighted contribution; title dominates, case number strong, snippet weak
    private const int TitleExact = 1000;
    private const int TitlePrefix = 600;
    private const int TitleWord = 450;       // whole-word match inside the title
    private const int TitleContains = 300;
    private const int CaseExact = 500;
    private const int CaseContains = 200;
    private const int SnippetContains = 80;
    private const int TokenCoverageMax = 200; // scaled by fraction of query tokens found
    private const int FuzzyBase = 120;        // near-typo baseline, reduced by edit distance

    /// <summary>Ranks search hits by relevance to the query (desc), title as tie-break; unchanged for empty query or single hit.</summary>
    public static List<SearchHit> Rank(string query, List<SearchHit> hits)
    {
        if (hits.Count < 2 || string.IsNullOrWhiteSpace(query))
        {
            return hits;
        }
        var q = query.Trim().ToLowerInvariant();
        var tokens = TextSimilarity.Tokens(query);
        return hits
            .Select(h => (h, s: Score(q, tokens, h.Title, h.CaseNumber, h.Snippet)))
            .OrderByDescending(x => x.s)
            .ThenBy(x => x.h.Title, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.h)
            .ToList();
    }

    /// <summary>Ranks command-palette hits by relevance (Name + case number only).</summary>
    public static List<QuickHit> RankQuick(string query, List<QuickHit> hits)
    {
        if (hits.Count < 2 || string.IsNullOrWhiteSpace(query))
        {
            return hits;
        }
        var q = query.Trim().ToLowerInvariant();
        var tokens = TextSimilarity.Tokens(query);
        return hits
            .Select(h => (h, s: Score(q, tokens, h.Name, h.CaseNumber, string.Empty)))
            .OrderByDescending(x => x.s)
            .ThenBy(x => x.h.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.h)
            .ToList();
    }

    private static int Score(string q, IReadOnlyList<string> tokens, string? title, string? caseNumber, string? snippet)
    {
        var t = (title ?? string.Empty).ToLowerInvariant();
        var c = (caseNumber ?? string.Empty).ToLowerInvariant();
        var sn = (snippet ?? string.Empty).ToLowerInvariant();
        var score = 0;

        // title: exact > prefix > whole-word > substring
        if (t.Length > 0)
        {
            if (t == q) score += TitleExact;
            else if (t.StartsWith(q, StringComparison.Ordinal)) score += TitlePrefix;
            else if (ContainsWord(t, q)) score += TitleWord;
            else if (t.Contains(q, StringComparison.Ordinal)) score += TitleContains;
        }

        // case number: exact > substring
        if (c.Length > 0)
        {
            if (c == q) score += CaseExact;
            else if (c.Contains(q, StringComparison.Ordinal)) score += CaseContains;
        }

        // snippet: low-weight substring
        if (sn.Length > 0 && sn.Contains(q, StringComparison.Ordinal)) score += SnippetContains;

        // multi-word coverage across all fields
        if (tokens.Count > 1)
        {
            var found = 0;
            foreach (var tok in tokens)
            {
                if (t.Contains(tok, StringComparison.Ordinal)
                    || c.Contains(tok, StringComparison.Ordinal)
                    || sn.Contains(tok, StringComparison.Ordinal))
                {
                    found++;
                }
            }
            score += TokenCoverageMax * found / tokens.Count;
        }

        // fuzzy fallback: typo matches rank just below any literal match
        if (score == 0 && tokens.Count > 0)
        {
            var candidate = TextSimilarity.Tokens(title, caseNumber, snippet);
            if (candidate.Count > 0 && TextSimilarity.PhraseSimilar(tokens, candidate, out var sum))
            {
                score += Math.Max(1, FuzzyBase - sum * 20);
            }
        }

        return score;
    }

    // whole-word containment: query bounded by non-alphanumeric chars (or string edges)
    private static bool ContainsWord(string haystack, string needle)
    {
        if (needle.Length == 0)
        {
            return false;
        }
        var idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            var leftOk = idx == 0 || !char.IsLetterOrDigit(haystack[idx - 1]);
            var end = idx + needle.Length;
            var rightOk = end >= haystack.Length || !char.IsLetterOrDigit(haystack[end]);
            if (leftOk && rightOk)
            {
                return true;
            }
            idx++;
        }
        return false;
    }
}
