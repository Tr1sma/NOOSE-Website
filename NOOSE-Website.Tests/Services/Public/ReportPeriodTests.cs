using NOOSE_Website.Services.Public;

namespace NOOSE_Website.Tests.Services.Public;

/// <summary>The period of a released report, which is also its public address.</summary>
/// <remarks>
/// Strict on purpose: the value comes from an anonymous URL, and a lenient parser would give one month two addresses.
/// </remarks>
public class ReportPeriodTests
{
    [Fact]
    public void FormatAndParse_RoundTrip()
    {
        var text = ReportPeriod.Format(2026, 8);
        Assert.Equal("2026-08", text);

        Assert.True(ReportPeriod.TryParse(text, out var year, out var month));
        Assert.Equal(2026, year);
        Assert.Equal(8, month);
    }

    [Fact]
    public void Label_ReadsAsAMonth()
        => Assert.Equal("August 2026", ReportPeriod.Label(2026, 8));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("2026-8")]
    [InlineData("2026-008")]
    [InlineData("2026-00")]
    [InlineData("2026-13")]
    [InlineData("0000-05")]
    [InlineData("2026/08")]
    [InlineData("+026-08")]
    [InlineData(" 2026-08")]
    public void AnythingElse_IsRejected(string? value)
        => Assert.False(ReportPeriod.TryParse(value, out _, out _));
}
