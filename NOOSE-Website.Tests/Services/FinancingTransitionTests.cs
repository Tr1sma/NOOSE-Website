using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services;

/// <summary>The full stage matrix of a funding request; every write path checks against this table.</summary>
public class FinancingTransitionTests
{
    private static readonly (FinancingStatus From, FinancingStatus To)[] Allowed =
    [
        (FinancingStatus.Requested, FinancingStatus.Approved),
        (FinancingStatus.Requested, FinancingStatus.Rejected),
        (FinancingStatus.Requested, FinancingStatus.Withdrawn),
        (FinancingStatus.Approved, FinancingStatus.Paid),
        (FinancingStatus.Approved, FinancingStatus.Requested),
        (FinancingStatus.Approved, FinancingStatus.Rejected),
        (FinancingStatus.Rejected, FinancingStatus.Approved),
        (FinancingStatus.Paid, FinancingStatus.Approved),
    ];

    [Fact]
    public void Matrix_MatchesTheAllowedEdgesExactly()
    {
        foreach (var from in FinancingStatusDisplay.All)
        {
            foreach (var to in FinancingStatusDisplay.All)
            {
                var expected = Allowed.Contains((from, to));
                Assert.Equal(expected, FinancingService.IsTransitionAllowed(from, to));
            }
        }
    }

    [Fact]
    public void NoStageMayTransitionToItself()
    {
        foreach (var status in FinancingStatusDisplay.All)
        {
            Assert.False(FinancingService.IsTransitionAllowed(status, status));
        }
    }

    [Fact]
    public void Withdrawn_IsTerminal()
    {
        foreach (var to in FinancingStatusDisplay.All)
        {
            Assert.False(FinancingService.IsTransitionAllowed(FinancingStatus.Withdrawn, to));
        }
        Assert.True(FinancingStatusDisplay.IsTerminal(FinancingStatus.Withdrawn));
    }

    [Fact]
    public void OnlyApprovalAndPayout_ConsumeBudget()
    {
        Assert.True(FinancingStatusDisplay.ConsumesBudget(FinancingStatus.Approved));
        Assert.True(FinancingStatusDisplay.ConsumesBudget(FinancingStatus.Paid));
        Assert.False(FinancingStatusDisplay.ConsumesBudget(FinancingStatus.Requested));
        Assert.False(FinancingStatusDisplay.ConsumesBudget(FinancingStatus.Rejected));
        Assert.False(FinancingStatusDisplay.ConsumesBudget(FinancingStatus.Withdrawn));
    }

    [Fact]
    public void OnlyRequested_CountsAsOpen()
    {
        Assert.True(FinancingStatusDisplay.IsOpen(FinancingStatus.Requested));
        foreach (var status in FinancingStatusDisplay.All.Where(s => s != FinancingStatus.Requested))
        {
            Assert.False(FinancingStatusDisplay.IsOpen(status));
        }
    }

    [Fact]
    public void EveryStage_HasALabelAndAnIcon()
    {
        foreach (var status in FinancingStatusDisplay.All)
        {
            Assert.NotEqual("—", FinancingStatusDisplay.Name(status));
            Assert.False(string.IsNullOrWhiteSpace(FinancingStatusDisplay.Icon(status)));
        }
        Assert.Equal("—", FinancingStatusDisplay.Name((FinancingStatus)99));
    }

    [Fact]
    public void All_CoversEveryStageExactlyOnce()
    {
        var declared = Enum.GetValues<FinancingStatus>();
        Assert.Equal(declared.Length, FinancingStatusDisplay.All.Count);
        Assert.Equal(declared.Length, FinancingStatusDisplay.All.Distinct().Count());
    }
}
