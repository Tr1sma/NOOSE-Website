using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services;

/// <summary>The caption under a score sparkline: it states endpoints and difference, never a direction word.</summary>
public class ThreatTrendTextTests
{
    [Fact]
    public void Hint_SaysSoWhenThereIsNoHistoryAtAll()
    {
        Assert.Equal("Noch kein Verlauf – dafür braucht es mindestens zwei Bewertungen.",
            ThreatTrendText.Hint(null));
        Assert.Equal("Noch kein Verlauf – dafür braucht es mindestens zwei Bewertungen.",
            ThreatTrendText.Hint([]));
    }

    [Fact]
    public void Hint_TreatsASingleScoreAsNoTrend()
    {
        // one point draws no line either, so the caption must not promise one
        Assert.Equal("Noch kein Verlauf – dafür braucht es mindestens zwei Bewertungen.",
            ThreatTrendText.Hint([42]));
    }

    [Fact]
    public void Hint_SignsARise()
    {
        Assert.Equal("Score-Verlauf über 2 Bewertungen: 40 → 55 (+15)", ThreatTrendText.Hint([40, 55]));
    }

    [Fact]
    public void Hint_SignsAFall()
    {
        Assert.Equal("Score-Verlauf über 2 Bewertungen: 55 → 40 (-15)", ThreatTrendText.Hint([55, 40]));
    }

    [Fact]
    public void Hint_ReportsNoDifferenceWhenACurveReturnsToWhereItStarted()
    {
        // the reason no direction word is used: this curve rose and fell, and both "rising" and "flat" would lie
        Assert.Equal("Score-Verlauf über 3 Bewertungen: 50 → 50 (±0)", ThreatTrendText.Hint([50, 90, 50]));
    }

    [Fact]
    public void Hint_CountsEveryPointItWasGiven()
    {
        Assert.Equal("Score-Verlauf über 8 Bewertungen: 10 → 80 (+70)",
            ThreatTrendText.Hint([10, 20, 30, 40, 50, 60, 70, 80]));
    }
}
