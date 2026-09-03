using NOOSE_Website.Data.Entities.Recruiting;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services;

/// <summary>The deadline rule of a test attempt, which every path asks and none re-derives.</summary>
/// <remarks>Pure functions taking their own "now", so these need no clock and no database — which is the whole
/// reason the rule was extracted instead of being written out at four call sites.</remarks>
public sealed class TestDeadlineTests
{
    private static readonly DateTime Now = new(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc);

    // ---------- For ----------

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-5)]
    public void For_returnsNull_whenThereIsNoUsableLimit(int? minutes)
        => Assert.Null(TestDeadline.For(Now, minutes));

    [Fact]
    public void For_addsTheMinutesToTheStart()
        => Assert.Equal(Now.AddMinutes(30), TestDeadline.For(Now, 30));

    // ---------- IsExpired ----------

    [Fact]
    public void IsExpired_isFalse_withoutADeadline()
        => Assert.False(TestDeadline.IsExpired(null, Now));

    [Fact]
    public void IsExpired_isFalse_whileTimeIsLeft()
        => Assert.False(TestDeadline.IsExpired(Now.AddSeconds(1), Now));

    [Fact]
    public void IsExpired_isTrue_atTheDeadline()
        => Assert.True(TestDeadline.IsExpired(Now, Now));

    [Fact]
    public void IsExpired_isTrue_pastTheDeadline()
        => Assert.True(TestDeadline.IsExpired(Now.AddSeconds(-1), Now));

    // ---------- GraceOver ----------

    [Fact]
    public void GraceOver_isFalse_justPastTheDeadline()
    {
        // a hand-in seconds late still carries the applicant's answers
        var deadline = Now - TestDeadline.SubmitGrace + TimeSpan.FromSeconds(1);
        Assert.False(TestDeadline.GraceOver(deadline, Now));
    }

    [Fact]
    public void GraceOver_isTrue_onceTheWindowClosed()
        => Assert.True(TestDeadline.GraceOver(Now - TestDeadline.SubmitGrace, Now));

    [Fact]
    public void GraceOver_isFalse_withoutADeadline()
        => Assert.False(TestDeadline.GraceOver(null, Now));

    // ---------- IsClosing ----------

    [Fact]
    public void IsClosing_isTrue_insideTheFinalSeconds()
    {
        // the automatic hand-in fires here, so a blank mandatory answer must not be refused
        var deadline = Now + TestDeadline.AutoSubmitLead - TimeSpan.FromSeconds(1);
        Assert.True(TestDeadline.IsClosing(deadline, Now));
    }

    [Fact]
    public void IsClosing_isFalse_withTimeToSpare()
        => Assert.False(TestDeadline.IsClosing(Now.AddMinutes(1), Now));

    [Fact]
    public void IsClosing_isFalse_withoutADeadline()
        => Assert.False(TestDeadline.IsClosing(null, Now));

    [Fact]
    public void IsClosing_isTrue_pastTheDeadline()
        => Assert.True(TestDeadline.IsClosing(Now.AddSeconds(-1), Now));

    // ---------- Remaining ----------

    [Fact]
    public void Remaining_isNull_withoutADeadline()
        => Assert.Null(TestDeadline.Remaining(null, Now));

    [Fact]
    public void Remaining_neverGoesNegative()
        => Assert.Equal(TimeSpan.Zero, TestDeadline.Remaining(Now.AddMinutes(-5), Now));

    [Fact]
    public void Remaining_countsDownToTheDeadline()
        => Assert.Equal(TimeSpan.FromMinutes(7), TestDeadline.Remaining(Now.AddMinutes(7), Now));

    // ---------- AllowedMinutes ----------

    [Fact]
    public void AllowedMinutes_isNull_whileNothingIsFrozenAndTheTestHasNoLimit()
        => Assert.Null(TestDeadline.AllowedMinutes(new BewerbungTestAssignment(), null));

    [Fact]
    public void AllowedMinutes_fallsBackToTheTest_beforeTheStart()
    {
        var assignment = new BewerbungTestAssignment { ExtraMinutes = 10 };
        Assert.Equal(40, TestDeadline.AllowedMinutes(assignment, 30));
    }

    [Fact]
    public void AllowedMinutes_addsTheGrantExactlyOnce()
    {
        // the base is frozen at the start and an extension never rewrites it, so the two columns add up once
        var assignment = new BewerbungTestAssignment { TimeLimitMinutes = 30, ExtraMinutes = 10 };
        Assert.Equal(40, TestDeadline.AllowedMinutes(assignment));
        Assert.Equal(40, TestDeadline.AllowedMinutes(assignment, 30));
    }

    [Fact]
    public void AllowedMinutes_isNull_forAStartedAttemptWithoutALimit()
        => Assert.Null(TestDeadline.AllowedMinutes(new BewerbungTestAssignment { StartedAt = Now }));

    // ---------- UsedMinutes ----------

    [Fact]
    public void UsedMinutes_isNull_whileEitherEndIsMissing()
    {
        Assert.Null(TestDeadline.UsedMinutes(null, Now));
        Assert.Null(TestDeadline.UsedMinutes(Now, null));
    }

    [Fact]
    public void UsedMinutes_roundsTheSpan()
        => Assert.Equal(27, TestDeadline.UsedMinutes(Now, Now.AddMinutes(27).AddSeconds(10)));

    [Fact]
    public void UsedMinutes_neverGoesNegative()
        => Assert.Equal(0, TestDeadline.UsedMinutes(Now, Now.AddMinutes(-3)));

    // ---------- IsLive ----------

    [Fact]
    public void IsLive_isTrue_forARunningTimedAttempt()
    {
        var assignment = new BewerbungTestAssignment { StartedAt = Now.AddMinutes(-5), DeadlineAt = Now.AddMinutes(5) };
        Assert.True(TestDeadline.IsLive(assignment, Now));
    }

    [Fact]
    public void IsLive_isFalse_withoutALimit()
    {
        // otherwise a started but never submitted attempt would block the test's questions forever
        var assignment = new BewerbungTestAssignment { StartedAt = Now.AddMinutes(-5) };
        Assert.False(TestDeadline.IsLive(assignment, Now));
    }

    [Fact]
    public void IsLive_isFalse_onceTheDeadlinePassed()
    {
        var assignment = new BewerbungTestAssignment { StartedAt = Now.AddMinutes(-30), DeadlineAt = Now.AddMinutes(-1) };
        Assert.False(TestDeadline.IsLive(assignment, Now));
    }

    [Fact]
    public void IsLive_isFalse_beforeTheStartAndAfterTheHandIn()
    {
        Assert.False(TestDeadline.IsLive(new BewerbungTestAssignment { DeadlineAt = Now.AddMinutes(5) }, Now));
        Assert.False(TestDeadline.IsLive(
            new BewerbungTestAssignment { StartedAt = Now.AddMinutes(-5), DeadlineAt = Now.AddMinutes(5), CompletedAt = Now },
            Now));
    }

    // ---------- Clamp ----------

    [Fact]
    public void Clamp_keepsNullAsUnlimited() => Assert.Null(TestDeadline.Clamp(null));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Clamp_readsAnythingAtOrBelowZeroAsUnlimited(int minutes)
        => Assert.Null(TestDeadline.Clamp(minutes));

    [Fact]
    public void Clamp_capsAtTheCeiling()
        => Assert.Equal(TestDeadline.MaxMinutes, TestDeadline.Clamp(TestDeadline.MaxMinutes + 1));

    [Fact]
    public void Clamp_keepsAUsableValue() => Assert.Equal(30, TestDeadline.Clamp(30));
}
