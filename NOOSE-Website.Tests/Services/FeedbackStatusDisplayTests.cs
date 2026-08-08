using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Tests.Services;

/// <summary>Guards the display companion of <see cref="FeedbackStatus"/>.</summary>
public class FeedbackStatusDisplayTests
{
    [Fact]
    public void EveryStage_HasALabelAndAnIcon()
    {
        foreach (var status in FeedbackStatusDisplay.All)
        {
            Assert.NotEqual("—", FeedbackStatusDisplay.Name(status));
            Assert.False(string.IsNullOrWhiteSpace(FeedbackStatusDisplay.Icon(status)));
        }
        Assert.Equal("—", FeedbackStatusDisplay.Name((FeedbackStatus)99));
    }

    [Fact]
    public void All_CoversEveryStageExactlyOnce()
    {
        var declared = Enum.GetValues<FeedbackStatus>();
        Assert.Equal(declared.Length, FeedbackStatusDisplay.All.Count);
        Assert.Equal(declared.Length, FeedbackStatusDisplay.All.Distinct().Count());
    }
}
