using MudBlazor;

namespace NOOSE_Website.Models.Enums;

/// <summary>The national situation level the agency states on /lage.</summary>
/// <remarks>
/// An editorial statement, deliberately not <see cref="HazardLevel"/>: that one is
/// <c>HazardLevelLogic.From(score)</c>, the level of a single record derived from its threat score. Sharing the enum
/// would be a standing invitation to compute one from the other, and an aggregate over the score stock would both
/// reach into classified files and export the scoring mechanic in a derived shape. Same reason
/// <see cref="PublicFactionStanding"/> is never derived from a faction's internal classification.
/// </remarks>
public enum PublicSituationLevel
{
    Niedrig = 0,
    Erhoeht = 1,
    Hoch = 2,
    Kritisch = 3,
}

/// <summary>Labels, colours and the allowlist that turns a stored name back into a level.</summary>
public static class PublicSituationLevelDisplay
{
    /// <summary>What a level is called outside; also the value stored in the settings row.</summary>
    /// <remarks>
    /// The name is stored rather than the number: a bare "2" in the settings table says nothing to whoever reads it
    /// there, and the audit row on /nachweis shows exactly that value.
    /// </remarks>
    public static string Name(PublicSituationLevel level) => level switch
    {
        PublicSituationLevel.Niedrig => "Niedrig",
        PublicSituationLevel.Erhoeht => "Erhöht",
        PublicSituationLevel.Hoch => "Hoch",
        PublicSituationLevel.Kritisch => "Kritisch",
        _ => "—",
    };

    /// <summary>Four distinct visible colours; an invisible one would be worse than no level at all.</summary>
    public static Color Colour(PublicSituationLevel level) => level switch
    {
        PublicSituationLevel.Niedrig => Color.Success,
        PublicSituationLevel.Erhoeht => Color.Info,
        PublicSituationLevel.Hoch => Color.Warning,
        PublicSituationLevel.Kritisch => Color.Error,
        _ => Color.Default,
    };

    /// <summary>One short line describing what the level means for a reader.</summary>
    public static string Hint(PublicSituationLevel level) => level switch
    {
        PublicSituationLevel.Niedrig => "Keine besonderen Vorkommnisse.",
        PublicSituationLevel.Erhoeht => "Erhöhte Wachsamkeit empfohlen.",
        PublicSituationLevel.Hoch => "Konkrete Gefährdungslage. Anweisungen der Behörden beachten.",
        PublicSituationLevel.Kritisch => "Akute Gefahr. Öffentliche Bereiche meiden.",
        _ => string.Empty,
    };

    public static readonly IReadOnlyList<PublicSituationLevel> All = new[]
    {
        PublicSituationLevel.Niedrig,
        PublicSituationLevel.Erhoeht,
        PublicSituationLevel.Hoch,
        PublicSituationLevel.Kritisch,
    };

    /// <summary>The stored name as a level, or null for anything unknown.</summary>
    /// <remarks>
    /// An allowlist rather than <c>Enum.Parse</c>: the value comes out of a hand-editable key/value row, and a stray
    /// entry would otherwise throw on an [AllowAnonymous] page — the same failure class as an unparsable query value.
    /// Null means "nothing said", which the read path keeps as silence instead of turning into a level.
    /// </remarks>
    public static PublicSituationLevel? Parse(string? name)
    {
        foreach (var level in All)
        {
            if (string.Equals(Name(level), name, StringComparison.Ordinal))
            {
                return level;
            }
        }
        return null;
    }
}
