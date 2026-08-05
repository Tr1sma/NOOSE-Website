using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services;

/// <summary>Unit tests for the faction freshness facets: only members/stocks/activities/docs count, oldest wins.</summary>
public class FactionRecencyTests
{
    private static readonly DateTime Created = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static Faction Faction(DateTime? members = null, DateTime? stock = null,
        DateTime? activities = null, DateTime? docs = null, DateTime? modified = null)
        => new()
        {
            Id = "f1",
            CreatedAt = Created,
            ModifiedAt = modified,
            MembersRefreshedAt = members,
            StockRefreshedAt = stock,
            ActivitiesRefreshedAt = activities,
            DocsRefreshedAt = docs,
        };

    // ---------------------------------------------------------------- RefreshedAt

    [Fact]
    public void RefreshedAt_FallsBackToCreation_WhenFacetNeverRefreshed()
    {
        var faction = Faction(members: Created.AddDays(10));

        Assert.Equal(Created.AddDays(10), FactionRecency.RefreshedAt(faction, FactionRecencyFacet.Members));
        Assert.Equal(Created, FactionRecency.RefreshedAt(faction, FactionRecencyFacet.Stock));
        Assert.Equal(Created, FactionRecency.RefreshedAt(faction, FactionRecencyFacet.Activities));
        Assert.Equal(Created, FactionRecency.RefreshedAt(faction, FactionRecencyFacet.Docs));
    }

    // ---------------------------------------------------------------- Reference

    [Fact]
    public void Reference_ReturnsOldestFacetStamp()
    {
        var faction = Faction(
            members: Created.AddDays(40),
            stock: Created.AddDays(10),
            activities: Created.AddDays(30),
            docs: Created.AddDays(20));

        Assert.Equal(Created.AddDays(10), FactionRecency.Reference(faction));
    }

    [Fact]
    public void Reference_IgnoresModifiedAt()
    {
        // master-data edits stamp ModifiedAt but must not refresh the light
        var faction = Faction(
            members: Created.AddDays(5), stock: Created.AddDays(5),
            activities: Created.AddDays(5), docs: Created.AddDays(5),
            modified: Created.AddDays(500));

        Assert.Equal(Created.AddDays(5), FactionRecency.Reference(faction));
    }

    [Fact]
    public void Reference_UsesCreation_WhenNothingRefreshedYet()
    {
        Assert.Equal(Created, FactionRecency.Reference(Faction()));
    }

    [Fact]
    public void Reference_FromColumns_MatchesEntityOverload()
    {
        var faction = Faction(members: Created.AddDays(3), docs: Created.AddDays(1));

        Assert.Equal(
            FactionRecency.Reference(faction),
            FactionRecency.Reference(Created, faction.MembersRefreshedAt, faction.StockRefreshedAt,
                faction.ActivitiesRefreshedAt, faction.DocsRefreshedAt));
    }

    // ---------------------------------------------------------------- Oldest

    [Theory]
    [InlineData(1, 2, 3, 4, FactionRecencyFacet.Members)]
    [InlineData(4, 1, 3, 2, FactionRecencyFacet.Stock)]
    [InlineData(4, 3, 1, 2, FactionRecencyFacet.Activities)]
    [InlineData(4, 3, 2, 1, FactionRecencyFacet.Docs)]
    public void Oldest_NamesTheFacetDrivingTheLight(int members, int stock, int activities, int docs,
        FactionRecencyFacet expected)
    {
        var faction = Faction(
            members: Created.AddDays(members), stock: Created.AddDays(stock),
            activities: Created.AddDays(activities), docs: Created.AddDays(docs));

        Assert.Equal(expected, FactionRecency.Oldest(faction));
    }

    [Fact]
    public void Oldest_ResolvesTiesInDisplayOrder()
    {
        // all four fall back to creation => the first facet wins, so the label stays stable
        Assert.Equal(FactionRecencyFacet.Members, FactionRecency.Oldest(Faction()));
    }

    // ---------------------------------------------------------------- Facets

    [Fact]
    public void Facets_ReturnsAllFourInDisplayOrder_WithExactlyOneOldest()
    {
        var faction = Faction(
            members: Created.AddDays(9), stock: Created.AddDays(2),
            activities: Created.AddDays(9), docs: Created.AddDays(9));

        var facets = FactionRecency.Facets(faction);

        Assert.Equal(FactionRecencyFacetDisplay.All, facets.Select(f => f.Facet));
        Assert.Single(facets, f => f.IsOldest);
        Assert.Equal(FactionRecencyFacet.Stock, facets.Single(f => f.IsOldest).Facet);
        Assert.Equal(Created.AddDays(2), facets.Single(f => f.Facet == FactionRecencyFacet.Stock).RefreshedUtc);
    }

    // ---------------------------------------------------------------- ReferenceBefore

    [Fact]
    public void ReferenceBefore_MatchesTheMinimum_ForEveryFacet()
    {
        var cutoff = Created.AddDays(50);
        var before = FactionRecency.ReferenceBefore(cutoff).Compile();

        // one neglected facet is enough, even when everything else is current
        Assert.True(before(Faction(members: Created.AddDays(10), stock: Created.AddDays(90),
            activities: Created.AddDays(90), docs: Created.AddDays(90))));
        Assert.True(before(Faction(members: Created.AddDays(90), stock: Created.AddDays(90),
            activities: Created.AddDays(90), docs: Created.AddDays(10))));
        Assert.False(before(Faction(members: Created.AddDays(60), stock: Created.AddDays(70),
            activities: Created.AddDays(80), docs: Created.AddDays(90))));
    }

    [Fact]
    public void ReferenceBefore_TreatsNeverRefreshedAsCreation()
    {
        var before = FactionRecency.ReferenceBefore(Created.AddDays(1)).Compile();

        Assert.True(before(Faction()));
    }

    // ---------------------------------------------------------------- display labels

    [Fact]
    public void FacetDisplay_HasGermanLabelAndSectionSlugForEveryFacet()
    {
        foreach (var facet in FactionRecencyFacetDisplay.All)
        {
            Assert.NotEqual("Unbekannt", FactionRecencyFacetDisplay.Name(facet));
            Assert.NotEqual("stammdaten", FactionRecencyFacetDisplay.Slug(facet));
            Assert.NotEmpty(FactionRecencyFacetDisplay.Icon(facet));
        }
    }
}
