using NOOSE_Website.Services.Search;

namespace NOOSE_Website.Tests.Services;

/// <summary>The text a result row shows: a window around the match, not the head of the document.</summary>
public class SearchSnippetTests
{
    [Fact]
    public void Centres_the_window_on_the_match_rather_than_the_head()
    {
        var text = new string('a', 500) + " NADEL " + new string('b', 500);

        var snippet = SearchSnippet.Around(text, "NADEL");

        Assert.Contains("NADEL", snippet);
        Assert.StartsWith("…", snippet);
        Assert.EndsWith("…", snippet);
    }

    [Fact]
    public void Matches_case_insensitively()
    {
        Assert.Contains("Nadel", SearchSnippet.Around("Heuhaufen mit Nadel darin", "nadel"));
    }

    [Fact]
    public void No_leading_ellipsis_when_the_match_is_at_the_start()
    {
        var snippet = SearchSnippet.Around("Nadel " + new string('b', 500), "Nadel");

        Assert.StartsWith("Nadel", snippet);
    }

    [Fact]
    public void Falls_back_to_the_head_when_the_term_is_not_in_the_plain_text()
    {
        // the term may have matched inside an attribute, which the plain-text projection drops
        var snippet = SearchSnippet.Around("Ein kurzer Text ohne den Begriff", "farbe");

        Assert.Equal("Ein kurzer Text ohne den Begriff", snippet);
    }

    [Fact]
    public void Head_fallback_is_capped_and_marked()
    {
        var snippet = SearchSnippet.Around(new string('a', 5000), "nichts");

        Assert.True(snippet.Length <= SearchSnippet.HeadMax + 1);
        Assert.EndsWith("…", snippet);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Empty_input_yields_empty_output(string? plain)
    {
        Assert.Equal(string.Empty, SearchSnippet.Around(plain, "egal"));
    }

    [Fact]
    public void An_empty_query_yields_the_head_rather_than_throwing()
    {
        Assert.Equal("Kurzer Text", SearchSnippet.Around("Kurzer Text", ""));
    }
}
