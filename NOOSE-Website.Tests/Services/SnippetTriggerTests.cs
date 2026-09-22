using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services;

/// <summary>When a slash asks for a snippet. The field above it has no test, so this carries the rule.</summary>
public class SnippetTriggerTests
{
    // ==================== when it asks ====================

    [Fact]
    public void ASlashAtTheStartOfTheTextAsks()
        => Assert.Equal("obs", SnippetTrigger.Query("/obs"));

    [Fact]
    public void ASlashAfterASpaceAsks()
    {
        Assert.Equal("obs", SnippetTrigger.Query("Gesehen am Hafen. /obs"));
        Assert.Equal("obs", SnippetTrigger.Query("Zeile eins\n/obs"));
    }

    [Fact]
    public void ABareSlashAsksWithoutAQueryYet()
        // the picker waits for a letter, exactly as the @-picker does - an empty query would offer everything
        => Assert.Equal(string.Empty, SnippetTrigger.Query("/"));

    // ==================== when it stays quiet ====================

    [Fact]
    public void ASlashInsideAWordAsksNothing()
    {
        // the reason the word boundary exists: this is an address, not a request
        Assert.Null(SnippetTrigger.Query("Vinewood/Ost"));
        Assert.Null(SnippetTrigger.Query("12/05"));
        Assert.Null(SnippetTrigger.Query("Person/Fraktion"));
    }

    [Fact]
    public void ASlashThatIsNotAtTheEndAsksNothing()
        // anchored at the end like the mention trigger, because the insertion path works on the tail
        => Assert.Null(SnippetTrigger.Query("/obs und dann weiter"));

    [Fact]
    public void TextWithoutASlashAsksNothing()
    {
        Assert.Null(SnippetTrigger.Query("Nur ein Satz."));
        Assert.Null(SnippetTrigger.Query(string.Empty));
        Assert.Null(SnippetTrigger.Query(null));
    }

    [Fact]
    public void ASecondSlashEndsTheQuery()
        // two slashes in a row are a path, not a name
        => Assert.Null(SnippetTrigger.Query("/obs/nacht"));

    // ==================== inserting ====================

    [Fact]
    public void InsertingReplacesTheAskingSlash()
        => Assert.Equal("Observation ohne Feststellung.",
            SnippetTrigger.Insert("/obs", "Observation ohne Feststellung."));

    [Fact]
    public void InsertingKeepsWhatWasWrittenBefore()
    {
        // the space before the slash belongs to the agent, not to the trigger
        Assert.Equal("Am Hafen. Observation ohne Feststellung.",
            SnippetTrigger.Insert("Am Hafen. /obs", "Observation ohne Feststellung."));
        Assert.Equal("Zeile eins\nBaustein",
            SnippetTrigger.Insert("Zeile eins\n/ba", "Baustein"));
    }

    [Fact]
    public void InsertingReplacesABareSlashAndKeepsTheRest()
        => Assert.Equal("Vorher Text", SnippetTrigger.Insert("Vorher /", "Text"));

    [Fact]
    public void InsertingWithoutAnAskingSlashAppends()
    {
        Assert.Equal("VorherText", SnippetTrigger.Insert("Vorher", "Text"));
        Assert.Equal("Text", SnippetTrigger.Insert(null, "Text"));
        Assert.Equal("Vorher", SnippetTrigger.Insert("Vorher", null));
    }

    [Fact]
    public void InsertingRespectsTheFieldsOwnLimit()
    {
        // MaxLength on the text field only stops typing; a snippet written into the bound string walks past
        // it - and past the column behind it
        var lang = new string('a', 100);

        Assert.Equal(20, SnippetTrigger.Insert("/b", lang, 20).Length);
        Assert.Equal("Vorher " + new string('a', 13), SnippetTrigger.Insert("Vorher /b", lang, 20));
    }

    [Fact]
    public void InsertingWithoutALimitKeepsEverything()
    {
        var lang = new string('a', 100);

        Assert.Equal(100, SnippetTrigger.Insert("/b", lang).Length);
    }

    // ==================== the row in the list ====================

    [Fact]
    public void ThePreviewIsOneLine()
    {
        // the picker row is one line high; a snippet with paragraphs would push the list apart
        Assert.Equal("Erste Zeile Zweite Zeile", SnippetTrigger.Preview("Erste Zeile\nZweite Zeile"));
        Assert.Equal("Erste Zeile Zweite Zeile", SnippetTrigger.Preview("Erste Zeile\r\nZweite Zeile"));
    }

    [Fact]
    public void ThePreviewIsCutWhenItGetsLong()
    {
        var lang = new string('a', 200);

        var kurz = SnippetTrigger.Preview(lang);

        Assert.Equal(71, kurz.Length);
        Assert.EndsWith("…", kurz, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePreviewSurvivesNothing()
    {
        Assert.Equal(string.Empty, SnippetTrigger.Preview(null));
        Assert.Equal(string.Empty, SnippetTrigger.Preview("   "));
    }

    [Fact]
    public void InsertingLeavesASlashInsideAWordAlone()
        // the query said no, and the insert must agree with it - otherwise a mis-fire eats the street name
        => Assert.Equal("Vinewood/OstText", SnippetTrigger.Insert("Vinewood/Ost", "Text"));
}
