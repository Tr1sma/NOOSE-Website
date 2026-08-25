using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services.Public;

/// <summary>The one ranking rule behind both hazard lists.</summary>
/// <remarks>
/// Stated once because two surfaces read it: the organisations come from their own snapshot, the people from the
/// wanted board. Written out twice it would drift, and a ranking that disagrees with itself is worse than none.
/// <para>
/// <see cref="HazardLevel.No"/> drops out: a hazard list of entries without a hazard is a full page saying nothing.
/// The cap is named on the page, because a silent cut reads like completeness.
/// </para>
/// </remarks>
public static class HazardRanking
{
    /// <summary>How many entries a list shows.</summary>
    public const int Limit = 25;

    /// <summary>Highest level first, newest publication as the tie-break, capped.</summary>
    public static List<T> Rank<T>(IEnumerable<T> cards, Func<T, HazardLevel> level, Func<T, DateTime?> published)
        => cards
            .Where(c => level(c) != HazardLevel.No)
            .OrderByDescending(c => (int)level(c))
            .ThenByDescending(c => published(c) ?? DateTime.MinValue)
            .Take(Limit)
            .ToList();
}
