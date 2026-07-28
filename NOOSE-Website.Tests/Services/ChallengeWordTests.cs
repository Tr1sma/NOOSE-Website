using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services;

public class ChallengeWordTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(20)]
    public void Generate_returnsRequestedLength(int length)
        => Assert.Equal(length, ChallengeWord.Generate(length).Length);

    [Fact]
    public void Generate_defaultsToSixLetters()
        => Assert.Equal(6, ChallengeWord.Generate().Length);

    [Fact]
    public void Generate_usesOnlyNonConfusableUppercaseLetters()
    {
        // 200 draws make an accidental miss of a forbidden letter vanishingly unlikely
        var all = string.Concat(Enumerable.Range(0, 200).Select(_ => ChallengeWord.Generate(10)));

        Assert.All(all, c => Assert.InRange(c, 'A', 'Z'));
        Assert.DoesNotContain('I', all);
        Assert.DoesNotContain('O', all);
        Assert.DoesNotContain('Q', all);
    }

    [Fact]
    public void Generate_zeroOrNegativeThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChallengeWord.Generate(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChallengeWord.Generate(-1));
    }

    [Theory]
    [InlineData("ABCDE", true)]
    [InlineData("abcde", true)]
    [InlineData("  AbCdE  ", true)]
    [InlineData("ABCD", false)]
    [InlineData("ABCDEF", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Matches_isCaseInsensitiveAndTrims(string? typed, bool expected)
        => Assert.Equal(expected, ChallengeWord.Matches(typed, "ABCDE"));
}
