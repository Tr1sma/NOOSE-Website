using System.Text;

namespace NOOSE_Website.Services;

/// <summary>Word-based similarity helpers (Levenshtein) for global search; runs in-memory since MySQL/Pomelo cannot translate edit distance.</summary>
public static class TextSimilarity
{
    /// <summary>Search words shorter than this do not trigger fuzzy matches.</summary>
    public const int MinWordLength = 3;

    /// <summary>Allowed edit distance per word length: shorter words stricter.</summary>
    public static int Threshold(int wordLength) => wordLength <= 4 ? 1 : 2;

    /// <summary>Splits texts into distinct lowercase words at non-alphanumeric boundaries.</summary>
    public static IReadOnlyList<string> Tokens(params string?[] texts)
    {
        var quantity = new HashSet<string>(StringComparer.Ordinal);
        var word = new StringBuilder();
        foreach (var text in texts)
        {
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }
            foreach (var c in text)
            {
                if (char.IsLetterOrDigit(c))
                {
                    word.Append(char.ToLowerInvariant(c));
                }
                else if (word.Length > 0)
                {
                    quantity.Add(word.ToString());
                    word.Clear();
                }
            }
            if (word.Length > 0)
            {
                quantity.Add(word.ToString());
                word.Clear();
            }
        }
        return quantity.Count == 0 ? Array.Empty<string>() : quantity.ToList();
    }

    /// <summary>True if every long-enough search word has a candidate within its threshold; sumDistance ranks relevance (smaller = better).</summary>
    public static bool PhraseSimilar(IReadOnlyList<string> searchWords, IReadOnlyList<string> candidateWords, out int sumDistance)
    {
        sumDistance = 0;
        var someChecked = false;
        foreach (var searchWord in searchWords)
        {
            if (searchWord.Length < MinWordLength)
            {
                continue;
            }
            someChecked = true;
            var threshold = Threshold(searchWord.Length);
            var bestDistance = int.MaxValue;
            foreach (var candidate in candidateWords)
            {
                if (Math.Abs(candidate.Length - searchWord.Length) > threshold)
                {
                    continue; // length gap alone exceeds threshold
                }
                var d = Distance(searchWord, candidate, threshold);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    if (bestDistance == 0)
                    {
                        break;
                    }
                }
            }
            if (bestDistance > threshold)
            {
                return false; // no similar word for this term
            }
            sumDistance += bestDistance;
        }
        return someChecked;
    }

    /// <summary>Levenshtein distance with an upper bound; bails out early as maxDistance + 1 when a whole row exceeds it.</summary>
    public static int Distance(string a, string b, int maxDistance = int.MaxValue)
    {
        if (a.Length == 0)
        {
            return b.Length;
        }
        if (b.Length == 0)
        {
            return a.Length;
        }

        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            var rowsMin = current[0];
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
                if (current[j] < rowsMin)
                {
                    rowsMin = current[j];
                }
            }
            if (rowsMin > maxDistance)
            {
                return maxDistance + 1;
            }
            (previous, current) = (current, previous);
        }
        return previous[b.Length];
    }
}
