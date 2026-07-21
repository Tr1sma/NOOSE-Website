using Microsoft.Extensions.Configuration;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;
using Xunit;

namespace NOOSE_Website.Tests.Services;

public class MiscHelpersTests
{
    private static IConfiguration BuildConfig(params (string Key, string? Value)[] pairs)
    {
        var dict = new Dictionary<string, string?>();
        foreach (var (key, value) in pairs)
        {
            dict[key] = value;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(dict)
            .Build();
    }

    // --- BootstrapAdmins.Ids ---------------------------------------------

    [Fact]
    public void Ids_NoConfiguredKeys_ReturnsEmptySet()
    {
        var config = BuildConfig();

        var ids = BootstrapAdmins.Ids(config);

        Assert.Empty(ids);
    }

    [Fact]
    public void Ids_SingleIdConfigured_ContainsThatId()
    {
        var config = BuildConfig(("Bootstrap:AdminDiscordId", "1001"));

        var ids = BootstrapAdmins.Ids(config);

        Assert.Single(ids);
        Assert.Contains("1001", ids);
    }

    [Fact]
    public void Ids_SingleIdWithSurroundingWhitespace_IsTrimmed()
    {
        var config = BuildConfig(("Bootstrap:AdminDiscordId", "  1001  "));

        var ids = BootstrapAdmins.Ids(config);

        Assert.Single(ids);
        Assert.Contains("1001", ids);
        Assert.DoesNotContain("  1001  ", ids);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Ids_SingleIdBlank_IsIgnored(string blank)
    {
        var config = BuildConfig(("Bootstrap:AdminDiscordId", blank));

        var ids = BootstrapAdmins.Ids(config);

        Assert.Empty(ids);
    }

    [Fact]
    public void Ids_ListConfigured_ContainsAllTrimmedEntries()
    {
        var config = BuildConfig(
            ("Bootstrap:AdminDiscordIds:0", " 200 "),
            ("Bootstrap:AdminDiscordIds:1", "300"));

        var ids = BootstrapAdmins.Ids(config);

        Assert.Equal(2, ids.Count);
        Assert.Contains("200", ids);
        Assert.Contains("300", ids);
    }

    [Fact]
    public void Ids_ListWithBlankEntries_SkipsBlanks()
    {
        var config = BuildConfig(
            ("Bootstrap:AdminDiscordIds:0", "200"),
            ("Bootstrap:AdminDiscordIds:1", "   "),
            ("Bootstrap:AdminDiscordIds:2", ""));

        var ids = BootstrapAdmins.Ids(config);

        Assert.Single(ids);
        Assert.Contains("200", ids);
    }

    [Fact]
    public void Ids_SingleAndListOverlap_DeduplicatesAcrossSources()
    {
        var config = BuildConfig(
            ("Bootstrap:AdminDiscordId", "999"),
            ("Bootstrap:AdminDiscordIds:0", "999"),
            ("Bootstrap:AdminDiscordIds:1", " 999 "),
            ("Bootstrap:AdminDiscordIds:2", "888"));

        var ids = BootstrapAdmins.Ids(config);

        Assert.Equal(2, ids.Count);
        Assert.Contains("999", ids);
        Assert.Contains("888", ids);
    }

    [Fact]
    public void Ids_MergesSingleAndList()
    {
        var config = BuildConfig(
            ("Bootstrap:AdminDiscordId", "1"),
            ("Bootstrap:AdminDiscordIds:0", "2"),
            ("Bootstrap:AdminDiscordIds:1", "3"));

        var ids = BootstrapAdmins.Ids(config);

        Assert.Equal(3, ids.Count);
        Assert.Contains("1", ids);
        Assert.Contains("2", ids);
        Assert.Contains("3", ids);
    }

    [Fact]
    public void Ids_UsesOrdinalComparer_CaseSensitiveEntriesKeptSeparate()
    {
        var config = BuildConfig(
            ("Bootstrap:AdminDiscordIds:0", "abc"),
            ("Bootstrap:AdminDiscordIds:1", "ABC"));

        var ids = BootstrapAdmins.Ids(config);

        Assert.Equal(2, ids.Count);
        Assert.Contains("abc", ids);
        Assert.Contains("ABC", ids);
    }

    // --- BootstrapAdmins.Contains ----------------------------------------

    [Fact]
    public void Contains_ConfiguredSingleId_ReturnsTrue()
    {
        var config = BuildConfig(("Bootstrap:AdminDiscordId", "555"));

        Assert.True(BootstrapAdmins.Contains(config, "555"));
    }

    [Fact]
    public void Contains_ConfiguredListId_ReturnsTrue()
    {
        var config = BuildConfig(("Bootstrap:AdminDiscordIds:0", "777"));

        Assert.True(BootstrapAdmins.Contains(config, "777"));
    }

    [Fact]
    public void Contains_DiscordIdWithSurroundingWhitespace_IsTrimmedBeforeMatch()
    {
        var config = BuildConfig(("Bootstrap:AdminDiscordId", "555"));

        Assert.True(BootstrapAdmins.Contains(config, "  555  "));
    }

    [Fact]
    public void Contains_UnknownId_ReturnsFalse()
    {
        var config = BuildConfig(("Bootstrap:AdminDiscordId", "555"));

        Assert.False(BootstrapAdmins.Contains(config, "111"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Contains_NullOrBlankDiscordId_ReturnsFalse(string? discordId)
    {
        var config = BuildConfig(("Bootstrap:AdminDiscordId", "555"));

        Assert.False(BootstrapAdmins.Contains(config, discordId));
    }

    [Fact]
    public void Contains_CaseMismatch_ReturnsFalse_Ordinal()
    {
        var config = BuildConfig(("Bootstrap:AdminDiscordId", "abc"));

        Assert.False(BootstrapAdmins.Contains(config, "ABC"));
    }

    [Fact]
    public void Contains_NoConfiguredIds_ReturnsFalse()
    {
        var config = BuildConfig();

        Assert.False(BootstrapAdmins.Contains(config, "555"));
    }

    // --- StringExtensions.TrimToNull -------------------------------------

    [Fact]
    public void TrimToNull_Null_ReturnsNull()
    {
        Assert.Null(((string?)null).TrimToNull());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData(" \t \n ")]
    public void TrimToNull_EmptyOrWhitespace_ReturnsNull(string value)
    {
        Assert.Null(value.TrimToNull());
    }

    [Theory]
    [InlineData("  hello  ", "hello")]
    [InlineData("\thello\n", "hello")]
    [InlineData("hello ", "hello")]
    [InlineData(" hello", "hello")]
    public void TrimToNull_SurroundingWhitespace_ReturnsTrimmed(string value, string expected)
    {
        Assert.Equal(expected, value.TrimToNull());
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("a")]
    [InlineData("multi word value")]
    public void TrimToNull_NoSurroundingWhitespace_ReturnsUnchanged(string value)
    {
        Assert.Equal(value, value.TrimToNull());
    }

    [Fact]
    public void TrimToNull_InnerWhitespacePreserved()
    {
        Assert.Equal("a  b", "  a  b  ".TrimToNull());
    }

    // --- ThreatScoreConstants.Base ---------------------------------------

    [Theory]
    [InlineData(Classification.SecuredStateThreatening, 75)]
    [InlineData(Classification.SuspicionCase, 50)]
    [InlineData(Classification.ReviewCase, 12)]
    [InlineData(Classification.Unknown, 0)]
    public void Base_KnownClassifications_ReturnsExpectedBand(Classification classification, int expected)
    {
        Assert.Equal(expected, ThreatScoreConstants.Base(classification));
    }

    [Fact]
    public void Base_UndefinedClassificationValue_ReturnsFallbackZero()
    {
        Assert.Equal(0, ThreatScoreConstants.Base((Classification)99));
    }
}
