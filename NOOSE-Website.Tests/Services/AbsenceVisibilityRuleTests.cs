using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services;

/// <summary>The roster tier grants the row but not the free text: "who is away", never "why".</summary>
public class AbsenceVisibilityRuleTests
{
    [Fact]
    public void Team_scope_hides_a_peers_reason()
    {
        Assert.False(AbsenceVisibility.MayReadPrivateFields(AbsenceViewScope.Team, isOwnRow: false));
    }

    [Fact]
    public void Team_scope_still_shows_your_own_reason()
    {
        Assert.True(AbsenceVisibility.MayReadPrivateFields(AbsenceViewScope.Team, isOwnRow: true));
    }

    [Fact]
    public void Own_scope_only_ever_carries_own_rows_and_shows_them()
    {
        Assert.True(AbsenceVisibility.MayReadPrivateFields(AbsenceViewScope.Own, isOwnRow: true));
    }

    [Fact]
    public void All_scope_reads_every_reason()
    {
        Assert.True(AbsenceVisibility.MayReadPrivateFields(AbsenceViewScope.All, isOwnRow: false));
    }
}
