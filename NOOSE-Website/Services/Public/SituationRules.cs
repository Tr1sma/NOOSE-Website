namespace NOOSE_Website.Services.Public;

/// <summary>Limits of the published situation level.</summary>
public static class SituationRules
{
    /// <summary>Cap of the assessment text.</summary>
    /// <remarks>
    /// Short on purpose: the assessment is the sentence under the level, not an article. Anything longer belongs in a
    /// warning or a press release, both of which have a body, an editor and a publication step.
    /// </remarks>
    public const int MaxNote = 600;
}
