using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;
using Xunit;

namespace NOOSE_Website.Tests.Services;

/// <summary>The one rule that decides who shows on the Fahndung page — now shared by the panel and the assistant,
/// so it is pinned here rather than re-derived in either.</summary>
public class WantedBoardTests
{
    [Theory]
    [InlineData(true, 0, HazardLevel.Critical, true)]     // manually wanted is always on the board
    [InlineData(false, 90, HazardLevel.Critical, true)]   // score 90 => Critical, meets a Critical threshold
    [InlineData(false, 40, HazardLevel.Critical, false)]  // score 40 => Medium, below Critical
    [InlineData(false, 40, HazardLevel.Medium, true)]     // same score meets a Medium threshold
    [InlineData(false, null, HazardLevel.Low, false)]     // no score at all stays off
    public void IsOnBoard_IsTheManualFlagOrTheThreshold(bool wanted, int? score, HazardLevel threshold, bool expected)
        => Assert.Equal(expected, WantedBoard.IsOnBoard(wanted, score, threshold));

    [Fact]
    public void Reason_PrefersTheManualNoteOverTheScore()
    {
        Assert.Equal("manuell ausgeschrieben",
            WantedBoard.Reason(new Person { IsWanted = true, ThreatScore = 90 }, HazardLevel.Critical));
        Assert.Equal("ab Gefahrenstufe Kritisch",
            WantedBoard.Reason(new Person { IsWanted = false, ThreatScore = 90 }, HazardLevel.Critical));
    }
}
