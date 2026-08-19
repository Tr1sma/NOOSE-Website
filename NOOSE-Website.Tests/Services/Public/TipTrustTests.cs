using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services.Public;

namespace NOOSE_Website.Tests.Services.Public;

public class TipTrustTests
{
    [Theory]
    [InlineData(-3, 1)]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(9, 3)]
    [InlineData(10, 4)]
    [InlineData(500, 4)]
    public void The_tier_rises_with_confirmed_tips(int confirmed, int expected)
        => Assert.Equal(expected, TipTrust.Tier(confirmed));

    [Fact]
    public void Every_tier_has_a_German_label()
    {
        for (var tier = TipTrust.MinTier; tier <= TipTrust.MaxTier; tier++)
        {
            Assert.False(string.IsNullOrWhiteSpace(TipTrust.Label(tier)));
        }
        Assert.Equal(4, Enumerable.Range(TipTrust.MinTier, TipTrust.MaxTier)
            .Select(TipTrust.Label).Distinct().Count());
    }

    [Fact]
    public void The_base_quota_is_the_one_in_TipRules()
    {
        // one number, two names would drift: the constant stays the tier-one quota
        Assert.Equal(TipRules.PerDay, TipTrust.DailyQuota(TipTrust.MinTier));
        Assert.Equal(TipRules.PerDay, TipTrust.QuotaFor(0));
    }

    [Fact]
    public void A_higher_tier_never_gets_a_smaller_quota()
    {
        for (var tier = TipTrust.MinTier; tier < TipTrust.MaxTier; tier++)
        {
            Assert.True(TipTrust.DailyQuota(tier + 1) > TipTrust.DailyQuota(tier));
        }
    }

    [Fact]
    public void The_quota_follows_the_confirmed_count_through_the_tier()
        => Assert.Equal(TipTrust.DailyQuota(3), TipTrust.QuotaFor(5));
    // ---- query predicates and their in-memory twins ----

    [Fact]
    public void The_open_predicate_agrees_with_its_twin_for_every_status()
    {
        var query = TipRules.OpenRows.Compile();
        foreach (var status in Enum.GetValues<TipStatus>())
        {
            Assert.Equal(TipRules.IsOpen(status), query(new Hinweis { Status = status }));
        }
    }

    [Fact]
    public void The_confirmed_predicate_agrees_with_its_twin_for_every_status()
    {
        var query = TipRules.ConfirmedRows.Compile();
        foreach (var status in Enum.GetValues<TipStatus>())
        {
            Assert.Equal(TipRules.CountsAsConfirmed(status), query(new Hinweis { Status = status }));
        }
    }

    [Fact]
    public void Open_and_confirmed_are_disjoint_and_together_cover_every_status_but_the_discarded_one()
    {
        foreach (var status in Enum.GetValues<TipStatus>())
        {
            Assert.False(TipRules.IsOpen(status) && TipRules.CountsAsConfirmed(status));
        }
        Assert.True(TipRules.IsClosed(TipStatus.Verworfen));
        Assert.False(TipRules.CountsAsConfirmed(TipStatus.Verworfen));
    }

    [Fact]
    public void The_anonymity_predicate_agrees_with_its_twin()
    {
        var query = TipAnonymity.Disclosable.Compile();
        var resolved = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
        foreach (var wants in new[] { true, false })
        {
            foreach (var at in new DateTime?[] { null, resolved })
            {
                var row = new Hinweis { WantsAnonymity = wants, AnonymityResolvedAt = at };
                Assert.Equal(!TipAnonymity.IsHidden(wants, at), query(row));
            }
        }
    }
}
