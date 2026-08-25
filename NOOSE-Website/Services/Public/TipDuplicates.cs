namespace NOOSE_Website.Services.Public;

/// <summary>Groups tips that describe the same incident, so ten reports of one event read as one line.</summary>
/// <remarks>
/// In memory on purpose: Pomelo translates no edit distance, the same reason the fuzzy search runs in memory. The
/// measure is symmetric — <see cref="TextSimilarity.PhraseSimilar"/> demands a partner for every word of one side, so
/// two reports of one incident with different length would never match.
/// </remarks>
public static class TipDuplicates
{
    /// <summary>Share of mutually similar words at or above which two tips are the same incident.</summary>
    public const double Threshold = 0.6;

    /// <summary>Below this many meaningful words a text carries too little signal to group on.</summary>
    public const int MinTokens = 4;

    /// <summary>How far back a new tip looks for its group.</summary>
    public const int CandidateDays = 30;

    public const int CandidateCap = 300;

    public static double Similarity(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        var left = Significant(a);
        var right = Significant(b);
        if (left.Count < MinTokens || right.Count < MinTokens)
        {
            return 0d;
        }

        var forward = Covered(left, right) / (double)left.Count;
        var backward = Covered(right, left) / (double)right.Count;
        return (forward + backward) / 2d;
    }

    public static bool AreDuplicates(IReadOnlyList<string> a, IReadOnlyList<string> b)
        => Similarity(a, b) >= Threshold;

    private static List<string> Significant(IReadOnlyList<string> words)
        => words.Where(w => w.Length >= TextSimilarity.MinWordLength).ToList();

    private static int Covered(List<string> words, List<string> candidates)
    {
        var hits = 0;
        foreach (var word in words)
        {
            var threshold = TextSimilarity.Threshold(word.Length);
            foreach (var candidate in candidates)
            {
                if (Math.Abs(candidate.Length - word.Length) > threshold)
                {
                    continue; // length gap alone exceeds the threshold
                }
                if (TextSimilarity.Distance(word, candidate, threshold) <= threshold)
                {
                    hits++;
                    break;
                }
            }
        }
        return hits;
    }
}
