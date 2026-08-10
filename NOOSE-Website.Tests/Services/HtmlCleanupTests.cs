using NOOSE_Website.Services;
using Xunit;

namespace NOOSE_Website.Tests.Services;

public class HtmlCleanupTests
{
    // ---- null / empty / whitespace guard ----

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData(" \t \r\n  ")]
    public void Clean_NullEmptyOrWhitespace_ReturnsEmptyString(string? input)
    {
        Assert.Equal(string.Empty, HtmlCleanup.Clean(input));
    }

    [Fact]
    public void Clean_Null_NeverReturnsNull()
    {
        Assert.NotNull(HtmlCleanup.Clean(null));
    }

    // ---- the NOOSEI image placeholder ----

    [Fact]
    public void CleanAiPayload_KeepsTheImagePlaceholder_WhileCleanDropsIt()
    {
        const string html = "<p>Text</p><p><img data-noosei-bild=\"0\"></p>";

        Assert.Contains("data-noosei-bild=\"0\"", HtmlCleanup.CleanAiPayload(html));
        // everywhere else the marker is meaningless and has no business being stored
        Assert.DoesNotContain("data-noosei-bild", HtmlCleanup.Clean(html));
    }

    [Fact]
    public void CleanAiPayload_StillDropsAnUnknownScheme()
    {
        // why the marker is an attribute and not a fake src: src is a URI attribute and gets sanitized
        Assert.DoesNotContain("noosei-bild", HtmlCleanup.CleanAiPayload("<p><img src=\"noosei-bild:0\"></p>"));
        Assert.DoesNotContain("javascript", HtmlCleanup.CleanAiPayload("<p><a href=\"javascript:alert(1)\">x</a></p>"));
    }

    // ---- PlainText: markup out, readable text in ----

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \r\n ")]
    public void PlainText_NullEmptyOrWhitespace_ReturnsEmptyString(string? input)
    {
        Assert.Equal(string.Empty, HtmlCleanup.PlainText(input));
    }

    [Fact]
    public void PlainText_StripsTagsAndCollapsesWhitespace()
    {
        Assert.Equal("Hallo Welt", HtmlCleanup.PlainText("<p>Hallo</p>\n\n<p>  Welt  </p>"));
    }

    [Fact]
    public void PlainText_SeparatesAdjacentInlineElements()
    {
        // replacing a tag with "" glues neighbouring words: "abc" instead of "ab c"
        Assert.Equal("ab c", HtmlCleanup.PlainText("<b>ab</b> <i>c</i>"));
        Assert.Equal("ab c", HtmlCleanup.PlainText("<b>ab</b><i>c</i>"));
    }

    [Fact]
    public void PlainText_DecodesEntities()
    {
        Assert.Equal("Müller & Sohn", HtmlCleanup.PlainText("<p>M&uuml;ller &amp; Sohn</p>"));
        Assert.Equal("a b", HtmlCleanup.PlainText("<p>a&nbsp;b</p>"));
    }

    [Fact]
    public void PlainText_OfAnEmptyQuillDocument_IsEmpty()
    {
        // the emptiness probe the personnel notes rely on
        Assert.Equal(string.Empty, HtmlCleanup.PlainText("<p><br></p>"));
    }

    [Fact]
    public void PlainText_DropsAttributeValues()
    {
        // a term that only matched inside an attribute is not in the snippet — SearchSnippet falls back for it
        Assert.Equal("Text", HtmlCleanup.PlainText("<p style=\"color:red\" title=\"geheim\">Text</p>"));
    }

    // ---- plain text passthrough ----

    [Fact]
    public void Clean_PlainText_ReturnedUnchanged()
    {
        const string input = "Just plain text here";
        Assert.Equal(input, HtmlCleanup.Clean(input));
    }

    [Fact]
    public void Clean_PlainTextWithDigitsAndPunctuation_Preserved()
    {
        const string input = "Report 42, updated. All good!";
        Assert.Equal(input, HtmlCleanup.Clean(input));
    }

    // ---- safe formatting tags are kept ----

    [Theory]
    [InlineData("p")]
    [InlineData("span")]
    [InlineData("b")]
    [InlineData("strong")]
    [InlineData("i")]
    [InlineData("em")]
    [InlineData("u")]
    [InlineData("s")]
    [InlineData("h1")]
    [InlineData("h2")]
    [InlineData("h3")]
    [InlineData("blockquote")]
    [InlineData("pre")]
    [InlineData("code")]
    [InlineData("div")]
    public void Clean_AllowedFormattingTag_IsPreserved(string tag)
    {
        var result = HtmlCleanup.Clean($"<{tag}>content</{tag}>");

        Assert.Contains($"<{tag}", result);
        Assert.Contains("content", result);
    }

    [Fact]
    public void Clean_CustomAllowedContainTag_IsPreserved()
    {
        var result = HtmlCleanup.Clean("<contain>wrapped</contain>");

        Assert.Contains("contain", result);
        Assert.Contains("wrapped", result);
    }

    [Fact]
    public void Clean_UnorderedList_IsPreserved()
    {
        var result = HtmlCleanup.Clean("<ul><li>one</li><li>two</li></ul>");

        Assert.Contains("<ul", result);
        Assert.Contains("<li", result);
        Assert.Contains("one", result);
        Assert.Contains("two", result);
    }

    [Fact]
    public void Clean_OrderedList_IsPreserved()
    {
        var result = HtmlCleanup.Clean("<ol><li>alpha</li></ol>");

        Assert.Contains("<ol", result);
        Assert.Contains("<li", result);
        Assert.Contains("alpha", result);
    }

    [Fact]
    public void Clean_Table_IsPreserved()
    {
        var result = HtmlCleanup.Clean(
            "<table><thead><tr><th>H</th></tr></thead><tbody><tr><td>cell</td></tr></tbody></table>");

        Assert.Contains("<table", result);
        Assert.Contains("<td", result);
        Assert.Contains("cell", result);
    }

    [Fact]
    public void Clean_LineBreak_IsPreserved()
    {
        var result = HtmlCleanup.Clean("first<br>second");

        Assert.Contains("<br", result);
        Assert.Contains("first", result);
        Assert.Contains("second", result);
    }

    [Fact]
    public void Clean_NestedFormatting_IsPreserved()
    {
        var result = HtmlCleanup.Clean("<p><strong>Hi</strong> <em>there</em></p>");

        Assert.Contains("<strong", result);
        Assert.Contains("<em", result);
        Assert.Contains("Hi", result);
        Assert.Contains("there", result);
    }

    // ---- dangerous / disallowed tags are stripped ----

    [Fact]
    public void Clean_ScriptTag_RemovesScriptAndPayload()
    {
        var result = HtmlCleanup.Clean("<p>Hello</p><script>alert('xss')</script>");

        Assert.DoesNotContain("<script", result);
        Assert.DoesNotContain("alert", result);
        Assert.Contains("Hello", result);
    }

    [Fact]
    public void Clean_ScriptOnly_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, HtmlCleanup.Clean("<script>doEvil()</script>"));
    }

    [Theory]
    [InlineData("iframe")]
    [InlineData("object")]
    [InlineData("embed")]
    [InlineData("form")]
    [InlineData("input")]
    [InlineData("button")]
    [InlineData("style")]
    [InlineData("link")]
    [InlineData("meta")]
    [InlineData("video")]
    [InlineData("audio")]
    [InlineData("svg")]
    public void Clean_DisallowedTag_IsStripped(string tag)
    {
        var result = HtmlCleanup.Clean($"<{tag}>x</{tag}>");

        Assert.DoesNotContain($"<{tag}", result);
    }

    [Fact]
    public void Clean_ImageTag_IsPreservedWithSiblingText()
    {
        var result = HtmlCleanup.Clean("<img src=\"x.jpg\">Caption");

        Assert.Contains("<img", result);
        Assert.Contains("Caption", result);
    }

    // ---- images ----

    [Fact]
    public void Clean_ImageWithHttpsUrl_KeepsSrc()
    {
        var result = HtmlCleanup.Clean("<img src=\"https://example.com/bild.png\">");

        Assert.Contains("<img", result);
        Assert.Contains("https://example.com/bild.png", result);
    }

    [Fact]
    public void Clean_ImageWithDataUri_KeepsSrc()
    {
        // quill embeds images as base64 data URIs
        const string src = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

        var result = HtmlCleanup.Clean($"<img src=\"{src}\">");

        Assert.Contains("<img", result);
        Assert.Contains(src, result);
    }

    [Fact]
    public void Clean_ImageAltAndSizeAttributes_ArePreserved()
    {
        var result = HtmlCleanup.Clean("<img src=\"x.jpg\" alt=\"Lagebild\" width=\"200\">");

        Assert.Contains("alt", result);
        Assert.Contains("Lagebild", result);
        Assert.Contains("width", result);
    }

    [Fact]
    public void Clean_ImageEventHandler_IsRemoved()
    {
        var result = HtmlCleanup.Clean("<img src=\"x.jpg\" onerror=\"evil()\">");

        Assert.Contains("<img", result);
        Assert.DoesNotContain("onerror", result);
        Assert.DoesNotContain("evil", result);
    }

    [Fact]
    public void Clean_ImageWithJavascriptScheme_SrcIsRemoved()
    {
        var result = HtmlCleanup.Clean("<img src=\"javascript:alert(1)\">");

        Assert.DoesNotContain("javascript", result);
    }

    // ---- event-handler / disallowed attributes are stripped ----

    [Fact]
    public void Clean_InlineEventHandler_IsRemoved()
    {
        var result = HtmlCleanup.Clean("<p onclick=\"evil()\">text</p>");

        Assert.DoesNotContain("onclick", result);
        Assert.DoesNotContain("evil", result);
        Assert.Contains("text", result);
    }

    [Fact]
    public void Clean_DisallowedIdAttribute_IsRemoved()
    {
        var result = HtmlCleanup.Clean("<p id=\"main\">body</p>");

        Assert.DoesNotContain("main", result);
        Assert.Contains("<p", result);
        Assert.Contains("body", result);
    }

    // ---- allowed attributes are kept ----

    [Fact]
    public void Clean_ClassAttribute_IsPreserved()
    {
        var result = HtmlCleanup.Clean("<p class=\"lead\">x</p>");

        Assert.Contains("class", result);
        Assert.Contains("lead", result);
    }

    [Fact]
    public void Clean_ContenteditableAttribute_IsPreserved()
    {
        var result = HtmlCleanup.Clean("<div contenteditable=\"true\">x</div>");

        Assert.Contains("contenteditable", result);
    }

    [Fact]
    public void Clean_DataAttribute_IsPreserved()
    {
        var result = HtmlCleanup.Clean("<div data-w=\"5\">x</div>");

        Assert.Contains("data-w", result);
    }

    [Fact]
    public void Clean_TableColspanAttribute_IsPreserved()
    {
        var result = HtmlCleanup.Clean(
            "<table><tbody><tr><td colspan=\"2\">c</td></tr></tbody></table>");

        Assert.Contains("colspan", result);
    }

    // ---- link schemes ----

    [Fact]
    public void Clean_HttpsLink_KeepsHref()
    {
        var result = HtmlCleanup.Clean("<a href=\"https://example.com\">link</a>");

        Assert.Contains("href", result);
        Assert.Contains("https://example.com", result);
    }

    [Fact]
    public void Clean_HttpLink_KeepsHref()
    {
        var result = HtmlCleanup.Clean("<a href=\"http://example.com\">link</a>");

        Assert.Contains("http://example.com", result);
    }

    [Fact]
    public void Clean_MailtoLink_KeepsHref()
    {
        var result = HtmlCleanup.Clean("<a href=\"mailto:agent@noose.info\">mail</a>");

        Assert.Contains("mailto:", result);
    }

    [Fact]
    public void Clean_LinkTargetAndRel_ArePreserved()
    {
        var result = HtmlCleanup.Clean(
            "<a href=\"https://x.com\" target=\"_blank\" rel=\"noopener\">y</a>");

        Assert.Contains("target", result);
        Assert.Contains("rel", result);
    }

    [Fact]
    public void Clean_JavascriptScheme_HrefIsRemoved()
    {
        var result = HtmlCleanup.Clean("<a href=\"javascript:alert(1)\">click</a>");

        Assert.DoesNotContain("javascript", result);
        Assert.Contains("click", result);
    }

    [Theory]
    [InlineData("ftp://host/file")]
    [InlineData("data:text/html,<b>x</b>")]
    [InlineData("vbscript:msgbox(1)")]
    public void Clean_DisallowedScheme_HrefIsRemoved(string href)
    {
        var result = HtmlCleanup.Clean($"<a href=\"{href}\">link</a>");

        Assert.DoesNotContain("href", result);
        Assert.Contains("link", result);
    }

    // ---- CSS property filtering ----

    [Fact]
    public void Clean_AllowedCssColor_IsPreserved()
    {
        var result = HtmlCleanup.Clean("<span style=\"color: red\">x</span>");

        Assert.Contains("color", result);
    }

    [Fact]
    public void Clean_AllowedCssTextAlign_IsPreserved()
    {
        var result = HtmlCleanup.Clean("<p style=\"text-align: center\">x</p>");

        Assert.Contains("text-align", result);
    }

    [Theory]
    [InlineData("position: absolute", "absolute")]
    [InlineData("display: none", "none")]
    [InlineData("float: left", "left")]
    public void Clean_DisallowedCssProperty_IsRemoved(string style, string marker)
    {
        var result = HtmlCleanup.Clean($"<span style=\"{style}\">x</span>");

        Assert.DoesNotContain(marker, result);
        Assert.Contains("<span", result);
    }

    // ---- general robustness ----

    [Fact]
    public void Clean_MixedAllowedAndDangerousContent_KeepsSafeDropsRest()
    {
        var result = HtmlCleanup.Clean(
            "<p>Safe <b>bold</b></p><script>steal()</script><iframe src=\"http://evil\"></iframe>");

        Assert.Contains("<p", result);
        Assert.Contains("<b", result);
        Assert.Contains("bold", result);
        Assert.DoesNotContain("<script", result);
        Assert.DoesNotContain("steal", result);
        Assert.DoesNotContain("<iframe", result);
    }

    [Fact]
    public void Clean_AnyInput_NeverReturnsNull()
    {
        Assert.NotNull(HtmlCleanup.Clean("<b>x</b>"));
        Assert.NotNull(HtmlCleanup.Clean("<script>x</script>"));
        Assert.NotNull(HtmlCleanup.Clean("plain"));
    }
}
