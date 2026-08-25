using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Tests.Models;

/// <summary>The item module switch drops what it owns out of the shared snapshot, in one pass over all five lists.</summary>
public class PublicWantedBoardTests
{
    private static PublicWantedDetail Detail(string caseNumber, PublicWantedKind kind)
        => new(caseNumber, kind, caseNumber, null, false, HazardLevel.Medium, DateTime.UtcNow, "<p>x</p>", null,
            null, null, []);

    private static PublicWantedCard Card(PublicWantedDetail d)
        => new(d.CaseNumber, d.Kind, d.DisplayName, d.AliasText, d.HasPhoto, d.HazardLevel, d.PublishedAt, d.Hints);

    private static PublicWantedArchiveCard Archived(string caseNumber, PublicWantedKind kind)
        => new(caseNumber, kind, caseNumber, false, DateTime.UtcNow);

    private static PublicWantedBoard Mixed()
    {
        var person = Detail("FA-1", PublicWantedKind.Fahndung);
        var plate = Detail("FA-2", PublicWantedKind.Fahrzeug);
        var weapon = Detail("FA-3", PublicWantedKind.Waffe);
        var archive = new[]
        {
            Archived("FA-4", PublicWantedKind.Fahndung),
            Archived("FA-5", PublicWantedKind.Fahrzeug),
        };

        return new PublicWantedBoard(
            [Card(person), Card(plate), Card(weapon)],
            new Dictionary<string, PublicWantedDetail>(StringComparer.OrdinalIgnoreCase)
            {
                ["FA-1"] = person, ["FA-2"] = plate, ["FA-3"] = weapon,
            },
            archive,
            archive.ToDictionary(a => a.CaseNumber, a => a, StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, PublicBounty>(StringComparer.OrdinalIgnoreCase)
            {
                ["FA-1"] = new(5000m, false), ["FA-2"] = new(1000m, false),
            });
    }

    [Fact]
    public void WithoutItems_DropsTheCardsTheProfilesAndTheArchiveRows()
    {
        var board = Mixed().WithoutItems();

        Assert.Equal(["FA-1"], board.Cards.Select(c => c.CaseNumber));
        Assert.Equal(["FA-1"], board.ByCaseNumber.Keys.Order());
        Assert.Equal(["FA-4"], board.Archive.Select(a => a.CaseNumber));
        Assert.Equal(["FA-4"], board.CapturedByCaseNumber.Keys.Order());
    }

    [Fact]
    public void WithoutItems_TakesTheMoneyWithTheCard()
    {
        var board = Mixed().WithoutItems();

        // a plate advertised on a page that no longer lists it would be the failure a second cache key invites
        Assert.Null(board.BountyFor("FA-2"));
        Assert.Equal(5000m, board.BountyFor("FA-1")!.Total);
    }

    [Fact]
    public void WithoutItems_LeavesAPersonOnlyBoardUntouched()
    {
        var person = Detail("FA-1", PublicWantedKind.Fahndung);
        var board = new PublicWantedBoard([Card(person)],
            new Dictionary<string, PublicWantedDetail>(StringComparer.OrdinalIgnoreCase) { ["FA-1"] = person },
            [], new Dictionary<string, PublicWantedArchiveCard>(StringComparer.OrdinalIgnoreCase),
            PublicWantedBoard.NoBounties);

        Assert.Single(board.WithoutItems().Cards);
    }

    [Fact]
    public void WithoutItems_OnTheEmptyBoard_IsStillEmpty()
        => Assert.Empty(PublicWantedBoard.Empty.WithoutItems().Cards);
}
