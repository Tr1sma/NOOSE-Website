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
    /// <summary>The value written to the settings row. A stable key, never the German label.</summary>
    /// <remarks>
    /// A name rather than the number, because a bare "2" says nothing to whoever reads the settings table. But
    /// deliberately its own string, the way <c>WarnhinweisColourChoice</c> and <c>PublicIconChoice</c> separate their
    /// stored Name from their German Label: the label is UI text somebody will reword one day, and if the stored
    /// value followed it, every existing row would stop parsing and /lage would go dark without a single test
    /// failing. <c>PublicSituationLevelTests</c> pins these four strings for that reason.
    /// </remarks>
    public static string Key(PublicSituationLevel level) => level switch
    {
        PublicSituationLevel.Niedrig => "Niedrig",
        PublicSituationLevel.Erhoeht => "Erhoeht",
        PublicSituationLevel.Hoch => "Hoch",
        PublicSituationLevel.Kritisch => "Kritisch",
        _ => string.Empty,
    };

    /// <summary>What a level is called in the UI and on the public page; free to be reworded.</summary>
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

    /// <summary>The stored key as a level, or null for anything unknown.</summary>
    /// <remarks>
    /// An allowlist rather than <c>Enum.Parse</c>: the value comes out of a hand-editable key/value row, and a stray
    /// entry would otherwise throw on an [AllowAnonymous] page — the same failure class as an unparsable query value.
    /// Null means "nothing said", which the read path keeps as silence instead of turning into a level. Matches the
    /// stored key only, never the label — that separation is the whole point of having both.
    /// </remarks>
    public static PublicSituationLevel? Parse(string? key)
    {
        foreach (var level in All)
        {
            if (string.Equals(Key(level), key, StringComparison.Ordinal))
            {
                return level;
            }
        }
        return null;
    }
}
