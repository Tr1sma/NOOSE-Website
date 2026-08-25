using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services.Public;

namespace NOOSE_Website.Tests.Services.Public;

/// <summary>The split rule: which share pays which tip, and what a payout may not be.</summary>
/// <remarks>
/// Pure and in-memory, so the arithmetic is pinned without a database. The order matters as much as the sum: a payout
/// must produce the same bookings every time, and money that needs an agent to hand cash over is drawn on last.
/// </remarks>
public class RewardAllocationTests
{
    private static readonly DateTime Base = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

    private static RewardAllocation.ShareCapacity Official(string id, decimal amount, int dayOffset = 0)
        => new(id, amount, BountyOrigin.NooseKasse, BountyShareStatus.Zugesagt, KassenKonto.Gruengeld,
            Base.AddDays(dayOffset));

    private static RewardAllocation.ShareCapacity Pledged(string id, decimal amount, int dayOffset = 0)
        => new(id, amount, BountyOrigin.AgentPrivat, BountyShareStatus.Zugesagt, null, Base.AddDays(dayOffset));

    private static RewardAllocation.ShareCapacity Secured(string id, decimal amount, int dayOffset = 0)
        => new(id, amount, BountyOrigin.AgentPrivat, BountyShareStatus.Gesichert, KassenKonto.Schwarzgeld,
            Base.AddDays(dayOffset));

    private static RewardAllocation.TipDemand Tip(string id, decimal amount) => new(id, amount);

    // ---- which shares move money ----

    [Fact]
    public void Agency_money_always_needs_a_booking()
    {
        Assert.True(RewardAllocation.NeedsBooking(BountyOrigin.NooseKasse, BountyShareStatus.Zugesagt));
    }

    [Fact]
    public void A_pledged_private_share_needs_none()
    {
        // it never reached the till, so the donor hands his own money over
        Assert.False(RewardAllocation.NeedsBooking(BountyOrigin.AgentPrivat, BountyShareStatus.Zugesagt));
    }

    [Fact]
    public void A_secured_private_share_needs_one()
    {
        Assert.True(RewardAllocation.NeedsBooking(BountyOrigin.AgentPrivat, BountyShareStatus.Gesichert));
    }

    // ---- order ----

    [Fact]
    public void The_personal_handover_is_drawn_on_last()
    {
        var order = RewardAllocation.Order([Pledged("pledged", 100m, dayOffset: -10), Official("agency", 100m)]);

        Assert.Equal(["agency", "pledged"], order.Select(s => s.ShareId));
    }

    [Fact]
    public void Within_a_group_the_oldest_share_goes_first()
    {
        var order = RewardAllocation.Order([Official("young", 100m, 5), Official("old", 100m, 1)]);

        Assert.Equal(["old", "young"], order.Select(s => s.ShareId));
    }

    [Fact]
    public void An_equal_timestamp_is_broken_by_the_id()
    {
        var order = RewardAllocation.Order([Official("b", 100m), Official("a", 100m)]);

        Assert.Equal(["a", "b"], order.Select(s => s.ShareId));
    }

    // ---- the split ----

    [Fact]
    public void A_single_tip_draws_from_the_first_share()
    {
        var slices = RewardAllocation.Distribute([Official("s1", 50_000m)], [Tip("t1", 20_000m)]);

        var slice = Assert.Single(slices);
        Assert.Equal("s1", slice.ShareId);
        Assert.Equal(20_000m, slice.Amount);
    }

    [Fact]
    public void A_split_across_three_tips_sums_up()
    {
        var slices = RewardAllocation.Distribute(
            [Official("s1", 60_000m)],
            [Tip("t1", 30_000m), Tip("t2", 20_000m), Tip("t3", 10_000m)]);

        Assert.Equal(60_000m, slices.Sum(s => s.Amount));
        Assert.Equal(3, slices.Select(s => s.TipId).Distinct().Count());
        Assert.All(slices, s => Assert.Equal("s1", s.ShareId));
    }

    [Fact]
    public void One_tip_spanning_two_shares_gets_one_slice_per_share()
    {
        var slices = RewardAllocation.Distribute(
            [Official("s1", 30_000m), Official("s2", 30_000m, 1)],
            [Tip("t1", 45_000m)]);

        Assert.Equal(2, slices.Count);
        Assert.Equal(30_000m, slices.Single(s => s.ShareId == "s1").Amount);
        Assert.Equal(15_000m, slices.Single(s => s.ShareId == "s2").Amount);
    }

    [Fact]
    public void The_bookable_money_is_spent_before_the_handover()
    {
        var slices = RewardAllocation.Distribute(
            [Pledged("pledged", 40_000m, -10), Official("agency", 40_000m)],
            [Tip("t1", 50_000m)]);

        Assert.Equal(40_000m, slices.Single(s => s.ShareId == "agency").Amount);
        Assert.Equal(10_000m, slices.Single(s => s.ShareId == "pledged").Amount);
    }

    [Fact]
    public void A_secured_share_counts_as_bookable_and_goes_before_a_pledge()
    {
        var slices = RewardAllocation.Distribute(
            [Pledged("pledged", 10_000m, -10), Secured("secured", 10_000m)],
            [Tip("t1", 10_000m)]);

        var slice = Assert.Single(slices);
        Assert.Equal("secured", slice.ShareId);
    }

    // ---- what a payout may not be ----

    [Fact]
    public void More_than_the_advertised_bounty_is_refused()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => RewardAllocation.Distribute([Official("s1", 10_000m)], [Tip("t1", 10_001m)]));

        Assert.Contains("übersteigt", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_amount_of_zero_is_refused()
    {
        Assert.Throws<InvalidOperationException>(
            () => RewardAllocation.Distribute([Official("s1", 10_000m)], [Tip("t1", 0m)]));
    }

    [Fact]
    public void A_negative_amount_is_refused()
    {
        Assert.Throws<InvalidOperationException>(
            () => RewardAllocation.Distribute([Official("s1", 10_000m)], [Tip("t1", -5m)]));
    }

    [Fact]
    public void A_third_decimal_is_refused_rather_than_truncated()
    {
        // the column holds two decimals; the database would swallow the rest without a word
        Assert.Throws<InvalidOperationException>(
            () => RewardAllocation.Distribute([Official("s1", 10_000m)], [Tip("t1", 1.005m)]));
    }

    [Fact]
    public void The_same_tip_twice_is_refused()
    {
        Assert.Throws<InvalidOperationException>(
            () => RewardAllocation.Distribute([Official("s1", 10_000m)], [Tip("t1", 100m), Tip("t1", 100m)]));
    }

    [Fact]
    public void No_tip_at_all_is_refused()
    {
        Assert.Throws<InvalidOperationException>(
            () => RewardAllocation.Distribute([Official("s1", 10_000m)], []));
    }

    [Fact]
    public void More_tips_than_the_dialog_holds_are_refused()
    {
        var tips = Enumerable.Range(0, RewardAllocation.MaxTips + 1)
            .Select(i => Tip($"t{i}", 1m))
            .ToList();

        Assert.Throws<InvalidOperationException>(
            () => RewardAllocation.Distribute([Official("s1", 10_000m)], tips));
    }

    [Fact]
    public void Without_a_share_there_is_nothing_to_pay()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => RewardAllocation.Distribute([], [Tip("t1", 100m)]));

        Assert.Contains("bereits ausgezahlt", error.Message, StringComparison.Ordinal);
    }
}
