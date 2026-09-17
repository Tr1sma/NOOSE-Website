namespace NOOSE_Website.Services;

/// <summary>Wording for a score sparkline, so a curve without numbers still says what it means.</summary>
/// <remarks>
/// Lives here rather than in the component because four views render the same curve, and a caption that drifts
/// between them is worse than none. The endpoints and the signed difference are stated instead of a word like
/// "rising": a curve that dips and returns ends where it started, and any direction word would lie about it.
/// </remarks>
public static class ThreatTrendText
{
    /// <summary>Tooltip for a list sparkline; too little history says so rather than inventing a trend.</summary>
    public static string Hint(IReadOnlyList<int>? values)
    {
        if (values is null || values.Count < 2)
        {
            return "Noch kein Verlauf – dafür braucht es mindestens zwei Bewertungen.";
        }

        var first = values[0];
        var last = values[^1];
        var delta = last - first;
        var change = delta switch
        {
            > 0 => $"+{delta}",
            < 0 => delta.ToString(),
            _ => "±0",
        };
        return $"Score-Verlauf über {values.Count} Bewertungen: {first} → {last} ({change})";
    }
}
