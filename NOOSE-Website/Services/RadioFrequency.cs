namespace NOOSE_Website.Services;

/// <summary>Frequency spelling, in one place for the plan, the page filter and the search provider.</summary>
public static class RadioFrequency
{
    /// <summary>German keyboards type a comma where the plan stores a dot. Used on every write.</summary>
    public static string Normalize(string? text) => (text ?? string.Empty).Trim().Replace(',', '.');

    /// <summary>The same needle written with a comma.</summary>
    /// <remarks>
    /// Needed for columns nothing normalised on write - <c>Faction.Radio</c> is typed into the faction record by
    /// hand, so the stored value may carry either mark. Comparing the stored value against both spellings of the
    /// query covers all four combinations; normalising only the query leaves a comma-stored frequency findable
    /// by neither spelling.
    /// </remarks>
    public static string Comma(string? text) => Normalize(text).Replace('.', ',');
}
