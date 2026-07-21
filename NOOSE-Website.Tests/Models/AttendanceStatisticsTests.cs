using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Statistics;
using Xunit;

namespace NOOSE_Website.Tests.Models;

public class AttendanceStatisticsTests
{
    // ---- From: Insufficient (evaluated < window) short-circuits everything ----

    [Fact]
    public void From_EvaluatedBelowWindow_ReturnsInsufficient()
    {
        var level = AttendanceAnomalyLogic.From(evaluated: 9, missing: 0, window: 10, yellow: 3, red: 6);
        Assert.Equal(AttendanceAnomalyLevel.Insufficient, level);
    }

    [Fact]
    public void From_EvaluatedOneBelowWindow_ReturnsInsufficient_boundary()
    {
        // window - 1 is still insufficient
        var level = AttendanceAnomalyLogic.From(evaluated: 9, missing: 100, window: 10, yellow: 3, red: 6);
        Assert.Equal(AttendanceAnomalyLevel.Insufficient, level);
    }

    [Fact]
    public void From_EvaluatedEqualsWindow_NotInsufficient_boundary()
    {
        // exactly a full window: judgement now possible; missing=0 -> None
        var level = AttendanceAnomalyLogic.From(evaluated: 10, missing: 0, window: 10, yellow: 3, red: 6);
        Assert.Equal(AttendanceAnomalyLevel.None, level);
    }

    [Fact]
    public void From_InsufficientTakesPrecedenceOverRed()
    {
        // even with missing well past red, too few evaluated -> Insufficient
        var level = AttendanceAnomalyLogic.From(evaluated: 5, missing: 999, window: 10, yellow: 3, red: 6);
        Assert.Equal(AttendanceAnomalyLevel.Insufficient, level);
    }

    [Fact]
    public void From_EvaluatedZeroWindowZero_NotInsufficient_boundary()
    {
        // 0 < 0 is false -> falls through to missing checks; missing=0 -> None
        var level = AttendanceAnomalyLogic.From(evaluated: 0, missing: 0, window: 0, yellow: 1, red: 1);
        Assert.Equal(AttendanceAnomalyLevel.None, level);
    }

    // ---- From: Red (missing >= red) ----

    [Fact]
    public void From_MissingEqualsRed_ReturnsRed_boundary()
    {
        var level = AttendanceAnomalyLogic.From(evaluated: 10, missing: 6, window: 10, yellow: 3, red: 6);
        Assert.Equal(AttendanceAnomalyLevel.Red, level);
    }

    [Fact]
    public void From_MissingAboveRed_ReturnsRed()
    {
        var level = AttendanceAnomalyLogic.From(evaluated: 10, missing: 7, window: 10, yellow: 3, red: 6);
        Assert.Equal(AttendanceAnomalyLevel.Red, level);
    }

    [Fact]
    public void From_MissingOneBelowRed_ReturnsYellow_boundary()
    {
        // red - 1, still >= yellow -> Yellow
        var level = AttendanceAnomalyLogic.From(evaluated: 10, missing: 5, window: 10, yellow: 3, red: 6);
        Assert.Equal(AttendanceAnomalyLevel.Yellow, level);
    }

    // ---- From: Yellow (missing >= yellow but < red) ----

    [Fact]
    public void From_MissingEqualsYellow_ReturnsYellow_boundary()
    {
        var level = AttendanceAnomalyLogic.From(evaluated: 10, missing: 3, window: 10, yellow: 3, red: 6);
        Assert.Equal(AttendanceAnomalyLevel.Yellow, level);
    }

    [Fact]
    public void From_MissingOneBelowYellow_ReturnsNone_boundary()
    {
        var level = AttendanceAnomalyLogic.From(evaluated: 10, missing: 2, window: 10, yellow: 3, red: 6);
        Assert.Equal(AttendanceAnomalyLevel.None, level);
    }

    // ---- From: None ----

    [Fact]
    public void From_MissingZero_ReturnsNone()
    {
        var level = AttendanceAnomalyLogic.From(evaluated: 20, missing: 0, window: 10, yellow: 3, red: 6);
        Assert.Equal(AttendanceAnomalyLevel.None, level);
    }

    // ---- From: adjacent yellow/red thresholds (red == yellow) ----

    [Fact]
    public void From_RedEqualsYellow_MissingAtThreshold_ReturnsRed()
    {
        // red checked before yellow; missing >= red wins
        var level = AttendanceAnomalyLogic.From(evaluated: 10, missing: 4, window: 10, yellow: 4, red: 4);
        Assert.Equal(AttendanceAnomalyLevel.Red, level);
    }

    [Fact]
    public void From_RedEqualsYellow_MissingBelow_ReturnsNone()
    {
        var level = AttendanceAnomalyLogic.From(evaluated: 10, missing: 3, window: 10, yellow: 4, red: 4);
        Assert.Equal(AttendanceAnomalyLevel.None, level);
    }

    // ---- From: table-driven full ladder (window=10, yellow=3, red=6) ----

    [Theory]
    [InlineData(9, 0, AttendanceAnomalyLevel.Insufficient)]   // below window
    [InlineData(10, 0, AttendanceAnomalyLevel.None)]          // full window, no misses
    [InlineData(10, 2, AttendanceAnomalyLevel.None)]          // just under yellow
    [InlineData(10, 3, AttendanceAnomalyLevel.Yellow)]        // yellow boundary
    [InlineData(10, 4, AttendanceAnomalyLevel.Yellow)]        // between yellow and red
    [InlineData(10, 5, AttendanceAnomalyLevel.Yellow)]        // just under red
    [InlineData(10, 6, AttendanceAnomalyLevel.Red)]           // red boundary
    [InlineData(10, 12, AttendanceAnomalyLevel.Red)]          // well past red
    [InlineData(0, 6, AttendanceAnomalyLevel.Insufficient)]   // no data at all
    public void From_ThresholdLadder_ClassifiesExpected(int evaluated, int missing, AttendanceAnomalyLevel expected)
    {
        var level = AttendanceAnomalyLogic.From(evaluated, missing, window: 10, yellow: 3, red: 6);
        Assert.Equal(expected, level);
    }

    // ---- Coherent: clamps into window >= red >= yellow >= 1 ----

    [Fact]
    public void Coherent_AlreadyCoherent_ReturnedUnchanged()
    {
        var (window, yellow, red) = AttendanceAnomalyLogic.Coherent(window: 10, yellow: 3, red: 6);
        Assert.Equal((10, 3, 6), (window, yellow, red));
    }

    [Fact]
    public void Coherent_YellowBelowOne_ClampedToOne()
    {
        var (window, yellow, red) = AttendanceAnomalyLogic.Coherent(window: 10, yellow: 0, red: 6);
        Assert.Equal((10, 1, 6), (window, yellow, red));
    }

    [Fact]
    public void Coherent_YellowAboveFifty_ClampedToFifty_CascadesUp()
    {
        // yellow->50, red->50, window->50
        var (window, yellow, red) = AttendanceAnomalyLogic.Coherent(window: 60, yellow: 100, red: 100);
        Assert.Equal((50, 50, 50), (window, yellow, red));
    }

    [Fact]
    public void Coherent_RedBelowYellow_RaisedToYellow()
    {
        var (window, yellow, red) = AttendanceAnomalyLogic.Coherent(window: 10, yellow: 5, red: 2);
        Assert.Equal((10, 5, 5), (window, yellow, red));
    }

    [Fact]
    public void Coherent_WindowBelowRed_RaisedToRed()
    {
        var (window, yellow, red) = AttendanceAnomalyLogic.Coherent(window: 2, yellow: 3, red: 6);
        Assert.Equal((6, 3, 6), (window, yellow, red));
    }

    [Fact]
    public void Coherent_AllNegative_ClampedToMinimums()
    {
        var (window, yellow, red) = AttendanceAnomalyLogic.Coherent(window: -5, yellow: -5, red: -5);
        Assert.Equal((1, 1, 1), (window, yellow, red));
    }

    [Theory]
    [InlineData(10, 3, 6, 10, 3, 6)]
    [InlineData(0, 0, 0, 1, 1, 1)]
    [InlineData(51, 51, 51, 50, 50, 50)]
    [InlineData(4, 7, 2, 7, 7, 7)]   // yellow=7 forces red>=7 forces window>=7
    [InlineData(50, 1, 1, 50, 1, 1)]
    public void Coherent_MaintainsWindowGeRedGeYellowGeOne(
        int window, int yellow, int red,
        int expWindow, int expYellow, int expRed)
    {
        var result = AttendanceAnomalyLogic.Coherent(window, yellow, red);
        Assert.Equal((expWindow, expYellow, expRed), result);
        // invariant holds
        Assert.True(result.Window >= result.Red);
        Assert.True(result.Red >= result.Yellow);
        Assert.True(result.Yellow >= 1);
    }
}
