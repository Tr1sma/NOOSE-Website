using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services;

/// <summary>Heading anchors and the table of contents share one slug rule, or a link misses its heading.</summary>
public sealed class RichTextAnchorsTests
{
    [Fact]
    public void ToStored_AssignsIdsToHeadings()
    {
        var stored = RichTextAnchors.ToStored("<h1>Lagebild</h1><p>x</p><h3>Hafen bei Nacht</h3>");

        Assert.Contains("id=\"lagebild\"", stored);
        Assert.Contains("id=\"hafen-bei-nacht\"", stored);
    }

    [Fact]
    public void ToStored_IsIdempotent()
    {
        var once = RichTextAnchors.ToStored("<h2>Lagebild</h2>");

        var twice = RichTextAnchors.ToStored(once);

        Assert.Equal(once, twice);
    }

    [Fact]
    public void ToStored_LeavesHtmlWithoutHeadingsUntouched()
    {
        const string html = "<P>GROSS</P>";

        Assert.Equal(html, RichTextAnchors.ToStored(html));
    }

    [Fact]
    public void ToStored_GivesDuplicatesASuffix()
    {
        var stored = RichTextAnchors.ToStored("<h2>Lage</h2><h2>Lage</h2><h2>Lage</h2>");

        Assert.Contains("id=\"lage\"", stored);
        Assert.Contains("id=\"lage-2\"", stored);
        Assert.Contains("id=\"lage-3\"", stored);
    }

    /// <summary>The sanitizer lets an id through on a heading, so a pasted one arrives here. Keeping it would
    /// hand an author a document-wide name of his choosing; only an id this rule could have written survives,
    /// which is what makes saving twice idempotent.</summary>
    [Fact]
    public void ToStored_ReplacesAnIdItWouldNotHaveWritten()
    {
        var stored = RichTextAnchors.ToStored("<h2 id=\"Fremder Wert!\">Lagebild</h2>");

        Assert.DoesNotContain("Fremder", stored);
        Assert.Contains("id=\"lagebild\"", stored);
    }

    /// <summary>An id this rule produced is kept, otherwise every save would renumber the anchors and break
    /// every link already written against them.</summary>
    [Fact]
    public void ToStored_KeepsAnIdItCouldHaveWritten()
    {
        var stored = RichTextAnchors.ToStored("<h2 id=\"lagebild-2\">Lagebild</h2>");

        Assert.Contains("id=\"lagebild-2\"", stored);
    }

    [Fact]
    public void Slug_TransliteratesGermanAndDropsPunctuation()
    {
        // same transliteration as HandbookService.Slug: ue/oe/ae/ss, never a bare vowel
        Assert.Equal("grosse-lage-ueber-dem-hafen", RichTextAnchors.Slug("Große Lage über dem Hafen!"));
        Assert.Equal("abschnitt", RichTextAnchors.Slug(""));
    }

    [Fact]
    public void BuildToc_UsesTheSameSlugsAsTheAnchors()
    {
        var toc = RichTextAnchors.BuildToc([
            new TocEntry(1, "Lagebild"),
            new TocEntry(2, "Große Lage über dem Hafen!"),
        ]);
        var stored = RichTextAnchors.ToStored("<h1>Lagebild</h1><h2>Große Lage über dem Hafen!</h2>");

        Assert.Contains("class=\"noose-inhaltsverzeichnis\"", toc);
        Assert.Contains("href=\"#lagebild\"", toc);
        Assert.Contains("href=\"#grosse-lage-ueber-dem-hafen\"", toc);
        Assert.Contains("id=\"lagebild\"", stored);
        Assert.Contains("id=\"grosse-lage-ueber-dem-hafen\"", stored);
    }

    [Fact]
    public void BuildToc_SkipsEmptyEntries()
    {
        var toc = RichTextAnchors.BuildToc([new TocEntry(1, "  "), new TocEntry(2, "Echt")]);

        Assert.Contains("Echt", toc);
        Assert.Equal(1, toc.Split("<li").Length - 1);
    }
}
