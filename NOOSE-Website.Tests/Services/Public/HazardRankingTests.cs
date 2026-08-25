using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Public;
using NOOSE_Website.Services.Public;

namespace NOOSE_Website.Tests.Services.Public;

/// <summary>The one ranking rule behind both hazard lists.</summary>
/// <remarks>
/// Tested against the real outward records rather than a stand-in, so a field the ranking reads cannot be removed
/// from one of them without this failing.
/// </remarks>
public class HazardRankingTests
{
    private static PublicFactionCard Faction(HazardLevel level, int day = 1)
        => new("Ballas", PublicFactionStanding.Beobachtet, level, null,
            new DateTime(2026, 1, day, 0, 0, 0, DateTimeKind.Utc));

    private static PublicWantedCard Person(HazardLevel level, int day = 1)
        => new($"FA-{day}", PublicWantedKind.Fahndung, "Max Mustermann", null, false, level,
            new DateTime(2026, 1, day, 0, 0, 0, DateTimeKind.Utc), []);

    [Fact]
    public void HighestLevelFirst()
    {
        var ranked = HazardRanking.Rank(
            [Faction(HazardLevel.Low), Faction(HazardLevel.Critical), Faction(HazardLevel.Medium)],
            c => c.HazardLevel, c => c.PublishedAt);

        Assert.Equal(
            new[] { HazardLevel.Critical, HazardLevel.Medium, HazardLevel.Low },
            ranked.Select(c => c.HazardLevel));
    }

    [Fact]
    public void OnATie_TheNewerPublicationWins()
    {
        var ranked = HazardRanking.Rank(
            [Faction(HazardLevel.High, day: 1), Faction(HazardLevel.High, day: 9)],
            c => c.HazardLevel, c => c.PublishedAt);

        Assert.Equal(new DateTime(2026, 1, 9, 0, 0, 0, DateTimeKind.Utc), ranked[0].PublishedAt);
    }

    [Fact]
    public void EntriesWithoutAHazard_DropOut()
    {
        var ranked = HazardRanking.Rank(
            [Faction(HazardLevel.No), Faction(HazardLevel.Low)],
            c => c.HazardLevel, c => c.PublishedAt);

        // a hazard list of entries without a hazard is a full page saying nothing
        Assert.Single(ranked);
        Assert.Equal(HazardLevel.Low, ranked[0].HazardLevel);
    }

    [Fact]
    public void AMissingPublicationDate_SortsLastRatherThanThrowing()
    {
        var undated = new PublicFactionCard("Vagos", PublicFactionStanding.Verboten, HazardLevel.High, null, null);

        var ranked = HazardRanking.Rank([undated, Faction(HazardLevel.High)], c => c.HazardLevel, c => c.PublishedAt);

        Assert.Equal("Ballas", ranked[0].DisplayName);
        Assert.Equal("Vagos", ranked[1].DisplayName);
    }

    [Fact]
    public void TheListIsCapped_AndTheCapIsOneNumber()
    {
        var many = Enumerable.Range(1, HazardRanking.Limit + 10)
            .Select(_ => Faction(HazardLevel.Medium))
            .ToList();

        Assert.Equal(HazardRanking.Limit, HazardRanking.Rank(many, c => c.HazardLevel, c => c.PublishedAt).Count);
    }

    [Fact]
    public void OnlyManhuntsBelongInThePeopleRanking()
    {
        // a missing person and a witness appeal carry a hazard level too; listing either under "most dangerous"
        // would be an accusation the notice never made. The page filters before ranking.
        var cards = new[]
        {
            Person(HazardLevel.Critical) with { Kind = PublicWantedKind.Vermisst },
            Person(HazardLevel.High, day: 2) with { Kind = PublicWantedKind.Zeugenaufruf },
            Person(HazardLevel.Low, day: 3),
        };

        var ranked = HazardRanking.Rank(cards.Where(c => c.Kind == PublicWantedKind.Fahndung),
            c => c.HazardLevel, c => c.PublishedAt);

        Assert.Equal(HazardLevel.Low, Assert.Single(ranked).HazardLevel);
    }

    [Fact]
    public void TheSameRuleRanksThePeopleList()
    {
        // the people ranking is a projection of the wanted board, not a second read path — so it must be the same rule
        var ranked = HazardRanking.Rank(
            [Person(HazardLevel.No), Person(HazardLevel.Low, day: 2), Person(HazardLevel.Critical, day: 3)],
            c => c.HazardLevel, c => c.PublishedAt);

        Assert.Equal(new[] { HazardLevel.Critical, HazardLevel.Low }, ranked.Select(c => c.HazardLevel));
    }
}
