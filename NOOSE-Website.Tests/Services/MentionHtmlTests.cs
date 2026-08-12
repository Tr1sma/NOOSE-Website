using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services;

public class MentionHtmlTests
{
    private const string Guid1 = "0f8fad5b-d9cb-469f-a165-70867728950e";
    private const string Guid2 = "1b9d6bcd-bbfd-4b2d-9b5d-ab8dfbbd4bed";

    private static string Token(string type, string id) => MentionParser.Token(type, id);

    private static Dictionary<(string, string), RecordsReference.Resolution> Map(
        string type, string id, string display, bool classified = false, string? href = "/personen/x")
        => new() { [(type, id)] = new RecordsReference.Resolution(display, classified, href) };

    // ---------- Refs ----------

    [Fact]
    public void Refs_NoToken_IsEmpty()
    {
        Assert.Empty(MentionHtml.Refs("<p>Nur Text ohne alles.</p>"));
    }

    [Fact]
    public void Refs_NullOrEmpty_IsEmpty()
    {
        Assert.Empty(MentionHtml.Refs(null));
        Assert.Empty(MentionHtml.Refs(string.Empty));
    }

    [Fact]
    public void Refs_FindsTokenInTextNode()
    {
        var refs = MentionHtml.Refs($"<p>Siehe {Token("Person", Guid1)} dazu.</p>");
        Assert.Equal(new[] { ("Person", Guid1) }, refs);
    }

    [Fact]
    public void Refs_Deduplicates()
    {
        var html = $"<p>{Token("Person", Guid1)}</p><p>{Token("Person", Guid1)}</p>";
        Assert.Single(MentionHtml.Refs(html));
    }

    [Fact]
    public void Refs_IgnoresTokenInAttribute()
    {
        Assert.Empty(MentionHtml.Refs($"<a href=\"/suche?q={Token("Person", Guid1)}\">Treffer</a>"));
    }

    // ---------- Rewrite: resolved ----------

    [Fact]
    public void Rewrite_Resolved_WritesLink()
    {
        var html = $"<p>Siehe {Token("Person", Guid1)}.</p>";
        var result = MentionHtml.Rewrite(html, Map("Person", Guid1, "Max Mustermann", href: "/personen/1"), isLeadership: false);

        Assert.Contains("<a class=\"erwaehnung\" href=\"/personen/1\">@Max Mustermann</a>", result);
        Assert.DoesNotContain("@{", result);
        Assert.Contains("Siehe ", result);
        Assert.Contains(".</p>", result);
    }

    [Fact]
    public void Rewrite_ResolvedWithoutHref_WritesChipNotLink()
    {
        var result = MentionHtml.Rewrite($"<p>{Token("Agent", Guid1)}</p>",
            Map("Agent", Guid1, "SHADOW", href: null), isLeadership: false);

        Assert.DoesNotContain("<a ", result);
        Assert.Contains("SHADOW", result);
    }

    // ---------- Rewrite: classified ----------

    [Fact]
    public void Rewrite_ClassifiedWithoutRight_HidesTheName()
    {
        var result = MentionHtml.Rewrite($"<p>{Token("Person", Guid1)}</p>",
            Map("Person", Guid1, "Geheime Person", classified: true), isLeadership: false);

        Assert.Contains("Verschlusssache", result);
        Assert.DoesNotContain("Geheime Person", result);
        Assert.DoesNotContain("<a ", result);
    }

    [Fact]
    public void Rewrite_ClassifiedWithRight_ShowsTheName()
    {
        var result = MentionHtml.Rewrite($"<p>{Token("Person", Guid1)}</p>",
            Map("Person", Guid1, "Geheime Person", classified: true), isLeadership: true);

        Assert.Contains("Geheime Person", result);
        Assert.DoesNotContain("Verschlusssache", result);
    }

    // ---------- Rewrite: unresolved ----------

    [Fact]
    public void Rewrite_Unresolved_WritesUnavailable()
    {
        var result = MentionHtml.Rewrite($"<p>{Token("Person", Guid2)}</p>",
            Map("Person", Guid1, "Max"), isLeadership: true);

        Assert.Contains("nicht verfügbar", result);
        Assert.DoesNotContain("Max", result);
        Assert.DoesNotContain("@{", result);
    }

    // ---------- Rewrite: plain ----------

    [Fact]
    public void Rewrite_Plain_WritesBareTextWithoutMarkup()
    {
        var result = MentionHtml.Rewrite($"<p>{Token("Person", Guid1)}</p>",
            Map("Person", Guid1, "Max Mustermann"), isLeadership: false, plain: true);

        Assert.Contains("Max Mustermann", result);
        Assert.DoesNotContain("erwaehnung", result);
        Assert.DoesNotContain("<a ", result);
    }

    // ---------- Rewrite: safety ----------

    [Fact]
    public void Rewrite_TokenInAttribute_StaysUntouched()
    {
        var html = $"<a href=\"/suche?q={Token("Person", Guid1)}\">Treffer</a>";
        var result = MentionHtml.Rewrite(html, Map("Person", Guid1, "Max"), isLeadership: true);

        Assert.Contains($"/suche?q={Token("Person", Guid1)}", result);
        Assert.DoesNotContain("class=\"erwaehnung\"", result);
    }

    [Fact]
    public void Rewrite_TokenInsideLink_DoesNotNestAnchors()
    {
        var html = $"<a href=\"/x\">Siehe {Token("Person", Guid1)}</a>";
        var result = MentionHtml.Rewrite(html, Map("Person", Guid1, "Max"), isLeadership: true);

        Assert.Equal(1, result.Split("<a ").Length - 1);
        Assert.Contains("Max", result);
    }

    [Fact]
    public void Rewrite_EncodesTheDisplayName()
    {
        var result = MentionHtml.Rewrite($"<p>{Token("Person", Guid1)}</p>",
            Map("Person", Guid1, "<script>alert(1)</script>"), isLeadership: true);

        Assert.DoesNotContain("<script>", result);
        Assert.Contains("&lt;script&gt;", result);
    }

    [Fact]
    public void Rewrite_KeepsSurroundingMarkup()
    {
        var html = $"<p><strong>Fett</strong> {Token("Person", Guid1)}</p><table><tr><td>Zelle</td></tr></table>";
        var result = MentionHtml.Rewrite(html, Map("Person", Guid1, "Max"), isLeadership: true);

        Assert.Contains("<strong>Fett</strong>", result);
        Assert.Contains("Zelle", result);
    }

    [Fact]
    public void Rewrite_NoToken_ReturnsInputVerbatim()
    {
        const string html = "<p>Ganz normaler Text.</p>";
        Assert.Equal(html, MentionHtml.Rewrite(html, new(), isLeadership: true));
    }

    // ---------- the token is plain text, so the existing pipeline carries it ----------

    [Fact]
    public void Clean_LeavesTheTokenIntact()
    {
        var cleaned = HtmlCleanup.Clean($"<p>Siehe {Token("Person", Guid1)}.</p>");
        Assert.Contains(Token("Person", Guid1), cleaned);
    }

    [Fact]
    public void PlainTextThenStrip_DropsTheToken()
    {
        var bare = MentionParser.Strip(HtmlCleanup.PlainText($"<p>Siehe {Token("Person", Guid1)} an.</p>"));
        Assert.DoesNotContain("@{", bare);
        Assert.Contains("Siehe", bare);
        Assert.Contains("an.", bare);
    }
}
