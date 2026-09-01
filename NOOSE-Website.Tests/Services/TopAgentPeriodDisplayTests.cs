using System.Globalization;
using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services;

/// <summary>Wording of the top-agent announcement; its marker doubles as the personnel-note de-duplication key.</summary>
public class TopAgentPeriodDisplayTests
{
    private static readonly DateTime Sample = new(2026, 9, 1); // KW 36

    private static readonly int[] AllBands =
        [1, 2, 6, 7, 8, 14, 27, 28, 30, 31, 32, 60, 89, 90, 92, 93, 364, 365, 366, 367];

    public static IEnumerable<object[]> Bands() => AllBands.Select(d => new object[] { d });

    [Theory]
    [InlineData(1, "des Tages")]
    [InlineData(7, "der Woche (KW 36)")]
    [InlineData(28, "des Monats")]
    [InlineData(30, "des Monats")]
    [InlineData(31, "des Monats")]
    [InlineData(90, "des Quartals")]
    [InlineData(92, "des Quartals")]
    [InlineData(365, "des Jahres")]
    [InlineData(366, "des Jahres")]
    [InlineData(2, "der letzten 2 Tage")]
    [InlineData(14, "der letzten 14 Tage")]
    [InlineData(27, "der letzten 27 Tage")]
    [InlineData(32, "der letzten 32 Tage")]
    [InlineData(89, "der letzten 89 Tage")]
    [InlineData(367, "der letzten 367 Tage")]
    public void For_DerivesTheHeadlineFromTheInterval(int days, string headline)
        => Assert.Equal(headline, TopAgentPeriodDisplay.For(days, Sample).Headline);

    [Fact]
    public void For_Weekly_KeepsTheShippedWordingByteForByte()
    {
        var w = TopAgentPeriodDisplay.For(7, Sample);

        Assert.Equal("der Woche (KW 36)", w.Headline);
        Assert.Equal("der Woche KW 36/2026", w.NotePhrase);
        Assert.Equal("KW 36/2026", w.Marker); // notes already on file de-dup against exactly this
    }

    [Fact]
    public void For_Weekly_LeavesASingleDigitWeekUnpadded()
        => Assert.Equal("KW 7/2026", TopAgentPeriodDisplay.For(7, new DateTime(2026, 2, 12)).Marker);

    [Fact]
    public void For_Weekly_UsesTheIsoYear()
        => Assert.Equal("KW 53/2026", TopAgentPeriodDisplay.For(7, new DateTime(2027, 1, 1)).Marker);

    [Fact]
    public void For_Monthly_NamesNoMonth()
    {
        // a rolling 30-day window is not a calendar month, so naming September would be a false statement
        var m = TopAgentPeriodDisplay.For(30, Sample);

        Assert.Equal("des Monats", m.Headline);
        Assert.Equal("des Monats (bis 01.09.2026)", m.NotePhrase);
        Assert.Equal("Monats (bis 01.09.2026)", m.Marker);
    }

    [Theory]
    [MemberData(nameof(Bands))]
    public void For_MarkerIsAlwaysASubstringOfTheNotePhrase(int days)
    {
        var w = TopAgentPeriodDisplay.For(days, Sample);

        Assert.Contains(w.Marker, w.NotePhrase);
    }

    [Fact]
    public void For_MarkersAreDistinctAcrossBands()
    {
        // one marker per band is intended (365 and 366 share it); two DIFFERENT bands sharing one merges two periods
        foreach (var group in AllBands.ToLookup(d => TopAgentPeriodDisplay.For(d, Sample).Marker))
        {
            var headlines = group.Select(d => TopAgentPeriodDisplay.For(d, Sample).Headline).Distinct();

            Assert.Single(headlines);
        }
    }

    [Fact]
    public void For_NoMarkerIsASubstringOfAnother()
    {
        // "4 Tage (bis X)" would sit inside "14 Tage (bis X)" and suppress the other period's note;
        // Distinct first, because equal markers inside one band are the intended case
        var markers = AllBands.Select(d => TopAgentPeriodDisplay.For(d, Sample).Marker).Distinct().ToList();

        foreach (var a in markers)
        {
            foreach (var b in markers.Where(other => other != a))
            {
                Assert.DoesNotContain(a, b);
            }
        }
    }

    [Fact]
    public void For_NonWeeklyMarkers_DifferBetweenRunDays()
    {
        // two runs can both clear the scheduler gate inside one calendar month, where a calendar-only marker collides
        Assert.NotEqual(
            TopAgentPeriodDisplay.For(30, Sample).Marker,
            TopAgentPeriodDisplay.For(30, Sample.AddDays(-29)).Marker);
    }

    [Theory]
    [InlineData(1, null)]
    [InlineData(7, "Week")]
    [InlineData(30, "Month")]
    [InlineData(90, null)]
    [InlineData(14, null)]
    public void For_LinksTheBoardFilterThatMatchesTheWording(int days, string? query)
        => Assert.Equal(query, TopAgentPeriodDisplay.For(days, Sample).PeriodQuery);

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void For_IsTotalForNonsenseIntervals(int days)
        => Assert.Equal("des Tages", TopAgentPeriodDisplay.For(days, Sample).Headline);

    [Theory]
    [MemberData(nameof(Bands))]
    public void For_IsCultureIndependent(int days)
    {
        // xunit runs under the machine culture, and in a custom format string a slash becomes the culture separator
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var german = TopAgentPeriodDisplay.For(days, Sample);
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            var english = TopAgentPeriodDisplay.For(days, Sample);

            Assert.Equal(german, english);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void For_Weekly_SeparatesWeekAndYearWithASlash()
        => Assert.Contains("/", TopAgentPeriodDisplay.For(7, Sample).Marker);

    [Fact]
    public void For_DatedBands_UseDotsInTheDate()
        => Assert.Contains("01.09.2026", TopAgentPeriodDisplay.For(30, Sample).Marker);
}
