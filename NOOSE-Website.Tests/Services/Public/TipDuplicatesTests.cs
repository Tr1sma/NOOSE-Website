using NOOSE_Website.Services;
using NOOSE_Website.Services.Public;

namespace NOOSE_Website.Tests.Services.Public;

public class TipDuplicatesTests
{
    private static bool Same(string a, string b)
        => TipDuplicates.AreDuplicates(TextSimilarity.Tokens(a), TextSimilarity.Tokens(b));

    private static double Score(string a, string b)
        => TipDuplicates.Similarity(TextSimilarity.Tokens(a), TextSimilarity.Tokens(b));

    [Fact]
    public void The_same_text_is_a_duplicate_of_itself()
    {
        const string text = "Der Gesuchte wurde heute Abend am Hafen von Los Santos gesehen, in einem blauen Wagen.";
        Assert.Equal(1d, Score(text, text));
        Assert.True(Same(text, text));
    }

    [Fact]
    public void A_reworded_report_of_one_incident_groups()
    {
        const string first = "Der Gesuchte wurde heute Abend am Hafen von Los Santos gesehen, in einem blauen Wagen.";
        const string second = "Heute Abend war der Gesuchte am Hafen von Los Santos, er saß in einem blauen Wagen.";
        Assert.True(Same(first, second));
    }

    [Fact]
    public void Typing_errors_do_not_split_a_group()
    {
        const string first = "Der Gesuchte wurde heute Abend am Hafen von Los Santos gesehen, in einem blauen Wagen.";
        const string second = "Der Gesuchte wurde heute Abnd am Hafn von Los Santos gesehen, in einem blaun Wagen.";
        Assert.True(Same(first, second));
    }

    [Fact]
    public void Two_different_incidents_stay_apart()
    {
        const string first = "Der Gesuchte wurde heute Abend am Hafen von Los Santos gesehen, in einem blauen Wagen.";
        const string second = "Vor der Bank in Paleto Bay stand ein Motorrad ohne Kennzeichen, zwei Männer warteten dort.";
        Assert.False(Same(first, second));
    }

    [Fact]
    public void The_measure_is_symmetric()
    {
        const string first = "Der Gesuchte wurde heute Abend am Hafen von Los Santos gesehen, in einem blauen Wagen.";
        const string second = "Heute Abend war der Gesuchte am Hafen von Los Santos, er saß in einem blauen Wagen.";
        Assert.Equal(Score(first, second), Score(second, first));
    }

    [Fact]
    public void A_short_report_inside_a_long_one_does_not_swallow_it()
    {
        // PhraseSimilar would match here: every word of the short text has a partner. A shared incident needs both
        // sides to describe the same thing, so the long report stays its own case.
        const string longer = "Der Gesuchte wurde heute Abend am Hafen von Los Santos gesehen, in einem blauen Wagen, "
            + "zusammen mit zwei anderen Männern, die Kisten aus einem Lieferwagen in ein Boot getragen haben.";
        const string shorter = "Der Gesuchte war am Hafen.";
        Assert.False(Same(longer, shorter));
    }

    [Fact]
    public void Too_little_text_is_never_grouped()
    {
        Assert.Equal(0d, Score("ab cd", "ab cd"));
        Assert.False(Same("Er war da", "Er war da"));
    }

    [Fact]
    public void The_threshold_is_the_documented_share()
    {
        Assert.Equal(0.6, TipDuplicates.Threshold);
        Assert.True(TipDuplicates.MinTokens >= TextSimilarity.MinWordLength);
    }
}
