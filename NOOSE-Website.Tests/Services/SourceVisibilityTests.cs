using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services;

/// <summary>The "nur intern" flag on a source: taskforce members only on a taskforce, never a partner anywhere.</summary>
public class SourceVisibilityTests
{
    private static Source Src(bool internalOnly) => new()
    {
        Id = "s1", EntityType = nameof(Taskforce), EntityId = "t1", Title = "Quelle", IsInternalOnly = internalOnly,
    };

    private static readonly IReadOnlySet<string> NoTaskforces = new HashSet<string>();
    private static readonly IReadOnlySet<string> MemberOfT1 = new HashSet<string> { "t1" };

    private static ViewerScope Agent(string me = "me") => new(false, false, me, null);
    private static ViewerScope Partner() => new(false, false, "p1", PartnerAgency.DoJ);

    [Fact]
    public void An_ordinary_source_is_untouched_by_this_gate()
    {
        Assert.True(SourceVisibility.MaySee(Src(false), nameof(Taskforce), "t1", Agent(), NoTaskforces));
        Assert.True(SourceVisibility.MaySee(Src(false), nameof(Person), "p1", Partner(), NoTaskforces));
    }

    [Fact]
    public void On_a_taskforce_only_members_see_an_internal_source()
    {
        Assert.False(SourceVisibility.MaySee(Src(true), nameof(Taskforce), "t1", Agent(), NoTaskforces));
        Assert.True(SourceVisibility.MaySee(Src(true), nameof(Taskforce), "t1", Agent(), MemberOfT1));
    }

    [Fact]
    public void Membership_in_another_taskforce_does_not_help()
    {
        Assert.False(SourceVisibility.MaySee(Src(true), nameof(Taskforce), "t2", Agent(), MemberOfT1));
    }

    [Fact]
    public void A_partner_never_sees_an_internal_source_even_on_a_released_person()
    {
        // this is the hardening: previously the flag was only consulted when the parent was a taskforce
        Assert.False(SourceVisibility.MaySee(Src(true), nameof(Person), "person-1", Partner(), NoTaskforces));
    }

    [Fact]
    public void On_a_non_taskforce_parent_an_internal_source_stays_visible_to_internal_agents()
    {
        // there is no owning taskforce to be a member of, so the second half of the rule has nothing to enforce
        Assert.True(SourceVisibility.MaySee(Src(true), nameof(Person), "person-1", Agent(), NoTaskforces));
    }

    [Fact]
    public void OnlyVisible_drops_exactly_the_sources_MaySee_rejects()
    {
        var sources = new List<Source> { Src(false), new() { Id = "s2", Title = "Intern", IsInternalOnly = true } };

        var kept = sources.OnlyVisible(nameof(Taskforce), "t1", Agent(), NoTaskforces);

        Assert.Equal(new[] { "s1" }, kept.Select(s => s.Id).ToArray());
    }
}
