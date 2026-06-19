using NOOSE_Website.Data.Entities.Recruiting;

namespace NOOSE_Website.Services;

/// <summary>Auto-grades recruiting test answers; free-text uses the same fuzzy matching as global search.</summary>
public static class TestGrading
{
    /// <summary>Multiple-choice: correct when the chosen option is flagged correct; null if unanswered.</summary>
    public static bool? GradeMultipleChoice(BewerbungTestOption? chosen) => chosen?.IsCorrect;

    /// <summary>Yes/No: null when no correct answer is defined; otherwise compares against it (unanswered = wrong).</summary>
    public static bool? GradeYesNo(string? answer, bool? correctYesNo)
    {
        if (correctYesNo is null)
        {
            return null;
        }
        return ParseYesNo(answer) == correctYesNo;
    }

    /// <summary>Free-text: counts keyword hits (fuzzy) and compares against the required minimum.</summary>
    public static (bool? correct, IReadOnlyList<string> matched, IReadOnlyList<string> missed) GradeFreeText(
        string? answer, string? keywordsRaw, int? minHits)
    {
        var entries = SplitKeywords(keywordsRaw);
        if (entries.Count == 0)
        {
            return (null, Array.Empty<string>(), Array.Empty<string>());
        }
        var answerTokens = TextSimilarity.Tokens(answer);
        var matched = new List<string>();
        var missed = new List<string>();
        foreach (var entry in entries)
        {
            if (KeywordHit(entry, answerTokens))
            {
                matched.Add(entry);
            }
            else
            {
                missed.Add(entry);
            }
        }
        var required = Math.Clamp(minHits ?? entries.Count, 1, entries.Count);
        return (matched.Count >= required, matched, missed);
    }

    private static bool KeywordHit(string entry, IReadOnlyList<string> answerTokens)
    {
        var entryTokens = TextSimilarity.Tokens(entry);
        if (entryTokens.Count == 0)
        {
            return false;
        }
        // long-enough tokens → fuzzy (search behaviour); all-short → exact presence
        if (entryTokens.Any(t => t.Length >= TextSimilarity.MinWordLength))
        {
            return TextSimilarity.PhraseSimilar(entryTokens, answerTokens, out _);
        }
        return entryTokens.All(answerTokens.Contains);
    }

    private static bool? ParseYesNo(string? answer)
    {
        var v = answer?.Trim();
        if (string.Equals(v, "Ja", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (string.Equals(v, "Nein", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return null;
    }

    private static List<string> SplitKeywords(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new List<string>();
        }
        return raw
            .Split(new[] { '\n', '\r', ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }
}
