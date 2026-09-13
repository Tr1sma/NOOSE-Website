using NOOSE_Website.Models.Handbook;
using NOOSE_Website.Services.Handbook;

namespace NOOSE_Website.Tests.Services;

/// <summary>The glossary pass over rendered HTML: what it marks, and the many things it must leave alone.</summary>
public sealed class GlossaryHtmlTests
{
    private static GlossaryTermView Term(string id, string term, string definition, string? synonyms = null)
        => new(id, term, synonyms, definition, null, null, null);

    private static GlossaryMatcher Matcher(params GlossaryTermView[] terms) => GlossaryMatcher.Build(terms);

    private static readonly GlossaryMatcher Standard = Matcher(
        Term("t-vs", "Verschlusssache", "Ein Inhalt, den nur die Führung sieht.", "VS"),
        Term("t-fahndung", "Fahndung", "Die öffentliche Ausschreibung einer Person."),
        Term("t-agent", "Agent", "Ein Mitglied der NOOSE."),
        Term("t-senior", "Senior Special Agent", "Der dritte Dienstgrad."));

    // --- what it marks ----------------------------------------------------

    [Fact]
    public void A_term_is_marked_once()
    {
        var html = GlossaryHtml.Annotate("<p>Eine Fahndung ist eine Fahndung.</p>", Standard);

        Assert.Equal(1, Occurrences(html, "class=\"glossar\""));
        Assert.Contains("data-glossar=\"Die öffentliche Ausschreibung einer Person.\"", html, StringComparison.Ordinal);
    }

    /// <summary>The reader's own wording survives; only the definition comes from the catalogue.</summary>
    [Fact]
    public void The_matched_text_is_kept_as_written()
    {
        var html = GlossaryHtml.Annotate("<p>eine fahndung läuft</p>", Standard);

        Assert.Contains(">fahndung</span>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void A_synonym_counts_as_the_same_term()
    {
        var html = GlossaryHtml.Annotate("<p>Eine Verschlusssache ist eine VS.</p>", Standard);

        // "VS" is the second spelling of a term already marked, so it stays plain
        Assert.Equal(1, Occurrences(html, "class=\"glossar\""));
    }

    [Fact]
    public void Two_different_terms_are_both_marked()
    {
        var html = GlossaryHtml.Annotate("<p>Ein Agent legt eine Fahndung an.</p>", Standard);

        Assert.Equal(2, Occurrences(html, "class=\"glossar\""));
    }

    /// <summary>Otherwise the page reads "Senior Special &lt;span&gt;Agent&lt;/span&gt;".</summary>
    [Fact]
    public void The_longest_term_wins()
    {
        var html = GlossaryHtml.Annotate("<p>Ein Senior Special Agent entscheidet.</p>", Standard);

        Assert.Contains(">Senior Special Agent</span>", html, StringComparison.Ordinal);
        Assert.Equal(1, Occurrences(html, "class=\"glossar\""));
    }

    /// <summary>A phrase already used is consumed whole: stepping into it would match the shorter term inside.</summary>
    [Fact]
    public void A_used_phrase_does_not_open_up_for_a_shorter_term_inside_it()
    {
        var html = GlossaryHtml.Annotate(
            "<p>Senior Special Agent und noch ein Senior Special Agent.</p>", Standard);

        Assert.Equal(1, Occurrences(html, "class=\"glossar\""));
        Assert.DoesNotContain(">Agent</span>", html, StringComparison.Ordinal);
    }

    // --- what it must leave alone -----------------------------------------

    /// <summary>German compounds: without a word boundary "Fahndung" lights up inside "Fahndungsliste".</summary>
    [Theory]
    [InlineData("<p>Die Fahndungsliste ist lang.</p>")]
    [InlineData("<p>Seine Agententätigkeit war bekannt.</p>")]
    [InlineData("<p>Vorfahndung gab es keine.</p>")]
    public void A_term_inside_a_longer_word_is_not_marked(string html)
        => Assert.DoesNotContain("glossar", GlossaryHtml.Annotate(html, Standard), StringComparison.Ordinal);

    [Fact]
    public void A_term_inside_a_link_is_left_alone()
    {
        var html = GlossaryHtml.Annotate("<p><a href=\"/x\">Zur Fahndung</a></p>", Standard);

        Assert.DoesNotContain("glossar", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("<p><code>Fahndung</code></p>")]
    [InlineData("<pre>Fahndung</pre>")]
    public void Quoted_content_is_left_alone(string html)
        => Assert.DoesNotContain("glossar", GlossaryHtml.Annotate(html, Standard), StringComparison.Ordinal);

    /// <summary>
    /// The mention pass runs first and emits a span, not a link, for a record the viewer may not see. Its label
    /// is itself a glossary word, and the link rule does not catch a span.
    /// </summary>
    [Fact]
    public void A_resolved_mention_is_left_alone()
    {
        var html = GlossaryHtml.Annotate(
            "<p><span class=\"erwaehnung erwaehnung-vs\">Verschlusssache</span></p>", Standard);

        Assert.DoesNotContain("glossar", html, StringComparison.Ordinal);
    }

    /// <summary>A regex over the markup would inject a span into the middle of the tag.</summary>
    [Fact]
    public void A_term_in_an_attribute_value_is_untouched()
    {
        var html = GlossaryHtml.Annotate(
            "<p><img src=\"/bilder/fahndung.png\" alt=\"Fahndung\" /></p>", Standard);

        Assert.Contains("src=\"/bilder/fahndung.png\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("glossar", html, StringComparison.Ordinal);
    }

    /// <summary>Re-serialising an untouched tree rewrites entities and quoting on every document.</summary>
    [Fact]
    public void Nothing_to_mark_returns_the_input_unchanged()
    {
        const string input = "<p>Hier steht gar nichts Besonderes.</p><table><tr><td>x</td></tr></table>";

        Assert.Same(input, GlossaryHtml.Annotate(input, Standard));
    }

    [Fact]
    public void An_empty_glossary_changes_nothing()
    {
        const string input = "<p>Eine Fahndung.</p>";

        Assert.Same(input, GlossaryHtml.Annotate(input, GlossaryMatcher.Empty));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Empty_input_is_survivable(string? input)
        => Assert.Equal(string.Empty, GlossaryHtml.Annotate(input, Standard));

    // --- encoding ---------------------------------------------------------

    /// <summary>The walk sees decoded text, so the replacement has to be re-encoded or the markup breaks.</summary>
    [Fact]
    public void Special_characters_around_a_match_are_re_encoded()
    {
        var html = GlossaryHtml.Annotate("<p>a &lt; b, eine Fahndung &amp; mehr</p>", Standard);

        Assert.Contains("&lt;", html, StringComparison.Ordinal);
        Assert.Contains("&amp;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<p>a < b", html, StringComparison.Ordinal);
    }

    [Fact]
    public void A_definition_with_a_quote_does_not_break_the_attribute()
    {
        var matcher = Matcher(Term("t", "Dok", "Das Protokoll einer \"Maßnahme\" an einer Person."));

        var html = GlossaryHtml.Annotate("<p>Ein Dok.</p>", matcher);

        Assert.Contains("&quot;", html, StringComparison.Ordinal);
        // the span still closes properly, so the following text is outside it
        Assert.Contains("</span>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Umlauts_survive_the_round_trip()
    {
        var html = GlossaryHtml.Annotate("<p>Die Überprüfung einer Fahndung in Köln.</p>", Standard);

        Assert.Contains("Überprüfung", html, StringComparison.Ordinal);
        Assert.Contains("Köln", html, StringComparison.Ordinal);
    }

    // --- the matcher itself -----------------------------------------------

    [Fact]
    public void A_term_without_a_definition_is_dropped()
    {
        var matcher = GlossaryMatcher.Build([Term("t", "Fahndung", "   ")]);

        Assert.True(matcher.IsEmpty);
    }

    [Fact]
    public void Synonyms_are_split_and_trimmed()
    {
        var matcher = Matcher(Term("t", "Aufsicht", "Liest alles, schreibt nichts.", " Teamleitung ,, Kontrolle "));

        Assert.NotNull(matcher.LongestAt("Die Teamleitung liest mit.", 4));
        Assert.NotNull(matcher.LongestAt("Die Kontrolle liest mit.", 4));
    }

    /// <summary>A hyphen has to count as a boundary, or a hyphenated term can never match at all.</summary>
    [Fact]
    public void A_hyphenated_term_matches()
    {
        var matcher = Matcher(Term("t", "Nur-Lese-Aufsicht", "Liest alles, schreibt nichts."));

        var hit = matcher.LongestAt("Die Nur-Lese-Aufsicht liest mit.", 4);

        Assert.NotNull(hit);
        Assert.Equal("Nur-Lese-Aufsicht", hit!.Phrase);
    }

    private static int Occurrences(string haystack, string needle)
    {
        var count = 0;
        var at = 0;
        while ((at = haystack.IndexOf(needle, at, StringComparison.Ordinal)) >= 0)
        {
            count++;
            at += needle.Length;
        }
        return count;
    }
}
