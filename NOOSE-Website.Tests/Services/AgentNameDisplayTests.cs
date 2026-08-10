using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services;

/// <summary>The one place that decides whether a viewer sees a codename or a real name.</summary>
public class AgentNameDisplayTests
{
    [Fact]
    public void Codename_wins_even_when_the_real_name_may_be_read()
    {
        Assert.Equal("Falke", AgentNameDisplay.Pick("Falke", "Max Mustermann", mayRealName: true));
    }

    [Fact]
    public void Without_codename_the_real_name_appears_only_for_viewers_allowed_to_see_it()
    {
        Assert.Equal("Max Mustermann", AgentNameDisplay.Pick(null, "Max Mustermann", mayRealName: true));
        Assert.Equal(AgentNameDisplay.Unnamed, AgentNameDisplay.Pick(null, "Max Mustermann", mayRealName: false));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_codename_is_treated_as_absent(string? codename)
    {
        Assert.Equal("Max Mustermann", AgentNameDisplay.Pick(codename, "Max Mustermann", mayRealName: true));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_real_name_falls_through_to_the_placeholder(string? realName)
    {
        Assert.Equal(AgentNameDisplay.Unnamed, AgentNameDisplay.Pick(null, realName, mayRealName: true));
    }

    [Fact]
    public void A_raw_id_is_never_the_answer()
    {
        Assert.Equal(AgentNameDisplay.Unnamed, AgentNameDisplay.Pick(null, null, mayRealName: true));
    }
}
