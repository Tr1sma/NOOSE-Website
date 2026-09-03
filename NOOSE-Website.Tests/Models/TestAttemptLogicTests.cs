using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Tests.Models;

/// <summary>Attempt state derived from the stored stamps, the branch four surfaces share.</summary>
/// <remarks>There is no bUnit in this suite, so a static helper is the only place this branching can be tested
/// at all — which is why the pages ask it instead of each deriving the state themselves.</remarks>
public sealed class TestAttemptLogicTests
{
    private static readonly DateTime Now = new(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void State_isNotStarted_whileNothingWasStamped()
        => Assert.Equal(TestAttemptState.NotStarted,
            TestAttemptLogic.State(null, null, null, timedOut: false, Now));

    [Fact]
    public void State_isRunning_whileTimeIsLeft()
        => Assert.Equal(TestAttemptState.Running,
            TestAttemptLogic.State(Now.AddMinutes(-5), Now.AddMinutes(5), null, timedOut: false, Now));

    [Fact]
    public void State_isRunning_forAStartedAttemptWithoutALimit()
        => Assert.Equal(TestAttemptState.Running,
            TestAttemptLogic.State(Now.AddMinutes(-5), null, null, timedOut: false, Now));

    [Fact]
    public void State_isExpired_atTheDeadline()
        => Assert.Equal(TestAttemptState.Expired,
            TestAttemptLogic.State(Now.AddMinutes(-30), Now, null, timedOut: false, Now));

    [Fact]
    public void State_isExpired_whenTheDeadlinePassedBeforeAnythingClosedIt()
    {
        // the gap between the deadline and the sweep: HRB must not keep reading "bearbeitet gerade"
        Assert.Equal(TestAttemptState.Expired,
            TestAttemptLogic.State(Now.AddMinutes(-30), Now.AddMinutes(-1), null, timedOut: false, Now));
    }

    [Fact]
    public void State_isSubmitted_whenTheHandInBeatTheClockByAHair()
    {
        // completed and not flagged: a submission that came in just in time must not read as a timeout, even
        // though the deadline is now in the past
        Assert.Equal(TestAttemptState.Submitted,
            TestAttemptLogic.State(Now.AddMinutes(-30), Now.AddMinutes(-1), Now.AddMinutes(-2), timedOut: false, Now));
    }

    [Fact]
    public void State_isExpired_onceTheClockClosedIt()
        => Assert.Equal(TestAttemptState.Expired,
            TestAttemptLogic.State(Now.AddMinutes(-30), Now.AddMinutes(-1), Now.AddMinutes(-1), timedOut: true, Now));

    [Fact]
    public void RemainingMinutes_isNull_withoutADeadlineOrOnceItRanOut()
    {
        Assert.Null(TestAttemptLogic.RemainingMinutes(null, Now));
        Assert.Null(TestAttemptLogic.RemainingMinutes(Now, Now));
        Assert.Null(TestAttemptLogic.RemainingMinutes(Now.AddMinutes(-1), Now));
    }

    [Fact]
    public void RemainingMinutes_roundsUp()
    {
        // 30 seconds left is still "1 Minute": rounding down would show 0 while the test is open
        Assert.Equal(1, TestAttemptLogic.RemainingMinutes(Now.AddSeconds(30), Now));
        Assert.Equal(13, TestAttemptLogic.RemainingMinutes(Now.AddMinutes(12).AddSeconds(10), Now));
    }
}
