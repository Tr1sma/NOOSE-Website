using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Tests.Models;

public class LifeStatusLogicTests
{
    // Fixed reference instant so all offset-based cases are deterministic.
    private static readonly DateTime Now = new(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc);

    // ---- RespawnMinutes constant ----

    [Fact]
    public void RespawnMinutes_is_twenty()
    {
        Assert.Equal(20, LifeStatusLogic.RespawnMinutes);
    }

    // ---- DeadUntilFrom ----

    [Fact]
    public void DeadUntilFrom_adds_twenty_minutes()
    {
        var reference = new DateTime(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc);

        var result = LifeStatusLogic.DeadUntilFrom(reference);

        Assert.Equal(new DateTime(2026, 7, 21, 12, 20, 0, DateTimeKind.Utc), result);
    }

    [Fact]
    public void DeadUntilFrom_equals_reference_plus_respawn_minutes()
    {
        var reference = new DateTime(2026, 1, 5, 8, 45, 30, DateTimeKind.Utc);

        var result = LifeStatusLogic.DeadUntilFrom(reference);

        Assert.Equal(reference.AddMinutes(LifeStatusLogic.RespawnMinutes), result);
    }

    [Fact]
    public void DeadUntilFrom_preserves_kind()
    {
        var reference = new DateTime(2026, 7, 21, 12, 0, 0, DateTimeKind.Local);

        var result = LifeStatusLogic.DeadUntilFrom(reference);

        Assert.Equal(DateTimeKind.Local, result.Kind);
    }

    [Fact]
    public void DeadUntilFrom_crosses_hour_boundary()
    {
        var reference = new DateTime(2026, 7, 21, 12, 50, 0, DateTimeKind.Utc);

        var result = LifeStatusLogic.DeadUntilFrom(reference);

        Assert.Equal(new DateTime(2026, 7, 21, 13, 10, 0, DateTimeKind.Utc), result);
    }

    // ---- Effective ----

    [Fact]
    public void Effective_dead_with_deadUntil_after_now_stays_dead()
    {
        var result = LifeStatusLogic.Effective(LifeStatus.Dead, Now.AddSeconds(1), Now);

        Assert.Equal(LifeStatus.Dead, result);
    }

    [Fact]
    public void Effective_dead_with_deadUntil_exactly_now_becomes_alive()
    {
        // t <= now boundary: equal timestamps count as expired.
        var result = LifeStatusLogic.Effective(LifeStatus.Dead, Now, Now);

        Assert.Equal(LifeStatus.Alive, result);
    }

    [Fact]
    public void Effective_dead_with_deadUntil_one_second_before_now_becomes_alive()
    {
        var result = LifeStatusLogic.Effective(LifeStatus.Dead, Now.AddSeconds(-1), Now);

        Assert.Equal(LifeStatus.Alive, result);
    }

    [Fact]
    public void Effective_dead_with_null_deadUntil_stays_dead()
    {
        var result = LifeStatusLogic.Effective(LifeStatus.Dead, null, Now);

        Assert.Equal(LifeStatus.Dead, result);
    }

    [Fact]
    public void Effective_alive_is_returned_unchanged_even_with_expired_deadUntil()
    {
        var result = LifeStatusLogic.Effective(LifeStatus.Alive, Now.AddMinutes(-5), Now);

        Assert.Equal(LifeStatus.Alive, result);
    }

    [Fact]
    public void Effective_fugitive_is_returned_unchanged_even_with_expired_deadUntil()
    {
        var result = LifeStatusLogic.Effective(LifeStatus.Fugitive, Now.AddMinutes(-5), Now);

        Assert.Equal(LifeStatus.Fugitive, result);
    }

    [Fact]
    public void Effective_fugitive_is_returned_unchanged_with_active_deadUntil()
    {
        var result = LifeStatusLogic.Effective(LifeStatus.Fugitive, Now.AddMinutes(5), Now);

        Assert.Equal(LifeStatus.Fugitive, result);
    }

    [Fact]
    public void Effective_alive_with_null_deadUntil_stays_alive()
    {
        var result = LifeStatusLogic.Effective(LifeStatus.Alive, null, Now);

        Assert.Equal(LifeStatus.Alive, result);
    }

    [Theory]
    [InlineData(-60, LifeStatus.Alive)]  // expired
    [InlineData(-1, LifeStatus.Alive)]   // 1s before now -> expired
    [InlineData(0, LifeStatus.Alive)]    // exactly now -> expired (t <= now)
    [InlineData(1, LifeStatus.Dead)]     // 1s after now -> still dead
    [InlineData(60, LifeStatus.Dead)]    // future
    public void Effective_dead_maps_by_deadUntil_offset(int deadUntilOffsetSeconds, LifeStatus expected)
    {
        var result = LifeStatusLogic.Effective(LifeStatus.Dead, Now.AddSeconds(deadUntilOffsetSeconds), Now);

        Assert.Equal(expected, result);
    }

    // ---- IsDeadWindow ----

    [Fact]
    public void IsDeadWindow_dead_with_deadUntil_after_now_is_true()
    {
        Assert.True(LifeStatusLogic.IsDeadWindow(LifeStatus.Dead, Now.AddSeconds(1), Now));
    }

    [Fact]
    public void IsDeadWindow_dead_with_deadUntil_exactly_now_is_false()
    {
        // t > now boundary: equal timestamps are not an active window.
        Assert.False(LifeStatusLogic.IsDeadWindow(LifeStatus.Dead, Now, Now));
    }

    [Fact]
    public void IsDeadWindow_dead_with_deadUntil_one_second_before_now_is_false()
    {
        Assert.False(LifeStatusLogic.IsDeadWindow(LifeStatus.Dead, Now.AddSeconds(-1), Now));
    }

    [Fact]
    public void IsDeadWindow_dead_with_null_deadUntil_is_false()
    {
        Assert.False(LifeStatusLogic.IsDeadWindow(LifeStatus.Dead, null, Now));
    }

    [Fact]
    public void IsDeadWindow_alive_with_active_deadUntil_is_false()
    {
        Assert.False(LifeStatusLogic.IsDeadWindow(LifeStatus.Alive, Now.AddMinutes(5), Now));
    }

    [Fact]
    public void IsDeadWindow_fugitive_with_active_deadUntil_is_false()
    {
        Assert.False(LifeStatusLogic.IsDeadWindow(LifeStatus.Fugitive, Now.AddMinutes(5), Now));
    }

    [Theory]
    [InlineData(-60, false)] // expired
    [InlineData(-1, false)]  // 1s before now
    [InlineData(0, false)]   // exactly now (t > now is false)
    [InlineData(1, true)]    // 1s after now
    [InlineData(60, true)]   // future
    public void IsDeadWindow_dead_maps_by_deadUntil_offset(int deadUntilOffsetSeconds, bool expected)
    {
        var result = LifeStatusLogic.IsDeadWindow(LifeStatus.Dead, Now.AddSeconds(deadUntilOffsetSeconds), Now);

        Assert.Equal(expected, result);
    }

    // ---- RemainingMinutes ----

    [Fact]
    public void RemainingMinutes_null_deadUntil_returns_null()
    {
        Assert.Null(LifeStatusLogic.RemainingMinutes(null, Now));
    }

    [Fact]
    public void RemainingMinutes_deadUntil_exactly_now_returns_null()
    {
        // t > now boundary: equal timestamps yield null.
        Assert.Null(LifeStatusLogic.RemainingMinutes(Now, Now));
    }

    [Fact]
    public void RemainingMinutes_deadUntil_one_second_before_now_returns_null()
    {
        Assert.Null(LifeStatusLogic.RemainingMinutes(Now.AddSeconds(-1), Now));
    }

    [Fact]
    public void RemainingMinutes_one_second_after_now_ceils_to_one()
    {
        Assert.Equal(1, LifeStatusLogic.RemainingMinutes(Now.AddSeconds(1), Now));
    }

    [Fact]
    public void RemainingMinutes_thirty_seconds_ceils_to_one()
    {
        Assert.Equal(1, LifeStatusLogic.RemainingMinutes(Now.AddSeconds(30), Now));
    }

    [Fact]
    public void RemainingMinutes_exactly_one_minute_is_one()
    {
        Assert.Equal(1, LifeStatusLogic.RemainingMinutes(Now.AddMinutes(1), Now));
    }

    [Fact]
    public void RemainingMinutes_sixty_one_seconds_ceils_to_two()
    {
        Assert.Equal(2, LifeStatusLogic.RemainingMinutes(Now.AddSeconds(61), Now));
    }

    [Fact]
    public void RemainingMinutes_full_respawn_window_is_twenty()
    {
        Assert.Equal(20, LifeStatusLogic.RemainingMinutes(Now.AddMinutes(20), Now));
    }

    [Fact]
    public void RemainingMinutes_nineteen_minutes_fifty_nine_seconds_ceils_to_twenty()
    {
        Assert.Equal(20, LifeStatusLogic.RemainingMinutes(Now.AddMinutes(19).AddSeconds(59), Now));
    }

    [Theory]
    [InlineData(-120, null)] // expired -> null
    [InlineData(-1, null)]   // 1s before now -> null
    [InlineData(0, null)]    // exactly now -> null (t > now false)
    [InlineData(1, 1)]       // 1s after -> ceil -> 1
    [InlineData(59, 1)]      // under a minute -> ceil -> 1
    [InlineData(60, 1)]      // exactly one minute -> 1
    [InlineData(61, 2)]      // just over a minute -> 2
    [InlineData(600, 10)]    // ten minutes exactly
    [InlineData(601, 11)]    // just over ten minutes -> 11
    [InlineData(1200, 20)]   // full respawn window
    public void RemainingMinutes_maps_offset_seconds_to_ceil_minutes(int deadUntilOffsetSeconds, int? expected)
    {
        var deadUntil = Now.AddSeconds(deadUntilOffsetSeconds);

        var result = LifeStatusLogic.RemainingMinutes(deadUntil, Now);

        Assert.Equal(expected, result);
    }
}
