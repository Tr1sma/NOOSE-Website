using NOOSE_Website.Services;

namespace NOOSE_Website.Tests.Services;

/// <summary>The correction pipeline's guarantee: the model never sees markup, so it cannot damage it.</summary>
public class TextBlocksTests
{
    [Fact]
    public void Parse_NumbersEveryLeafBlock()
    {
        var doc = TextBlocks.Parse("<p>Erster Absatz.</p><h2>Überschrift</h2><ul><li>Eins</li><li>Zwei</li></ul>");

        Assert.Equal([1, 2, 3, 4], doc.Blocks.Select(b => b.Number).ToArray());
        Assert.Equal(["Erster Absatz.", "Überschrift", "Eins", "Zwei"], doc.Blocks.Select(b => b.Text).ToArray());
    }

    [Fact]
    public void Parse_DoesNotCountAContainerTwice()
    {
        var doc = TextBlocks.Parse("<table><tr><td><p>In der Zelle</p></td></tr></table>");

        var block = Assert.Single(doc.Blocks);
        Assert.Equal("In der Zelle", block.Text);
    }

    [Fact]
    public void Parse_SkipsCodeBlocks()
    {
        var doc = TextBlocks.Parse("<p>Text</p><pre>var x = 1;</pre>");

        var block = Assert.Single(doc.Blocks);
        Assert.Equal("Text", block.Text);
    }

    [Fact]
    public void Parse_HandlesBareTextWithoutBlockElements()
    {
        var doc = TextBlocks.Parse("Einfach nur Text");

        Assert.Single(doc.Blocks);
        Assert.Equal("Einfach nur Text", doc.Blocks[0].Text);
    }

    [Fact]
    public void ToPrompt_NumbersTheLines()
    {
        var prompt = TextBlocks.Parse("<p>Eins</p><p>Zwei</p>").ToPrompt();

        Assert.Equal("[1] Eins\r\n[2] Zwei".ReplaceLineEndings(), prompt.ReplaceLineEndings());
    }

    // ---- answer parsing ----

    [Fact]
    public void ParseAnswer_ReadsNumberedLines()
    {
        var result = TextBlocks.ParseAnswer("[1] Erster Satz.\n[2] Zweiter Satz.", 2);

        Assert.NotNull(result);
        Assert.Equal("Erster Satz.", result![1]);
        Assert.Equal("Zweiter Satz.", result[2]);
    }

    [Fact]
    public void ParseAnswer_ToleratesPreambleAndFences()
    {
        var result = TextBlocks.ParseAnswer("Gern!\n```\n[1] Erster Satz.\n[2] Zweiter Satz.\n```\nSonst noch was?", 2);

        Assert.NotNull(result);
        Assert.Equal("Erster Satz.", result![1]);
        // trailing chatter is folded into the last block rather than becoming a phantom one
        Assert.Equal(2, result.Count);
    }

    [Theory]
    [InlineData("[1] Nur einer.", 2)]                       // too few
    [InlineData("[1] Eins\n[2] Zwei\n[3] Drei", 2)]         // too many
    [InlineData("[1] Eins\n[3] Drei", 2)]                   // gap
    [InlineData("[1] Eins\n[1] Nochmal", 2)]                // duplicate
    [InlineData("Ohne jede Nummerierung", 1)]
    [InlineData("", 1)]
    public void ParseAnswer_RejectsAnythingThatDoesNotLineUp(string answer, int expected)
        => Assert.Null(TextBlocks.ParseAnswer(answer, expected));

    // ---- writing back ----

    [Fact]
    public void Apply_CorrectsASimpleBlock()
    {
        var doc = TextBlocks.Parse("<p>Der Verdächtige wurde festgenomen.</p>");

        TextBlocks.Apply(doc.Blocks[0], "Der Verdächtige wurde festgenommen.");

        Assert.Equal("<p>Der Verdächtige wurde festgenommen.</p>", doc.ToHtml());
    }

    [Fact]
    public void Apply_KeepsInlineFormatting()
    {
        var doc = TextBlocks.Parse("<p>Der Verdächtige wurde <b>festgenomen</b> heute.</p>");

        TextBlocks.Apply(doc.Blocks[0], "Der Verdächtige wurde festgenommen heute.");

        var html = doc.ToHtml();
        Assert.Contains("<b>", html);
        Assert.Contains("festgenommen", html);
        Assert.DoesNotContain("festgenomen<", html);
    }

    [Fact]
    public void Apply_LeavesTheDocumentAloneWhenNothingChanged()
    {
        const string original = "<p>Alles korrekt.</p><ul><li>Eins</li></ul>";
        var doc = TextBlocks.Parse(original);

        foreach (var block in doc.Blocks)
        {
            TextBlocks.Apply(block, block.Text);
        }

        Assert.Equal(original, doc.ToHtml());
    }

    [Fact]
    public void Apply_NeverTouchesTheSurroundingStructure()
    {
        var doc = TextBlocks.Parse("<h2>Titel</h2><p>Ein Satz mit fehlar.</p><ul><li>Punkt</li></ul>");

        TextBlocks.Apply(doc.Blocks[1], "Ein Satz mit Fehler.");

        var html = doc.ToHtml();
        Assert.StartsWith("<h2>Titel</h2>", html);
        Assert.Contains("<ul><li>Punkt</li></ul>", html);
        Assert.Contains("Ein Satz mit Fehler.", html);
    }

    [Fact]
    public void Apply_IgnoresAnEmptyCorrection()
    {
        var doc = TextBlocks.Parse("<p>Bleibt stehen.</p>");

        TextBlocks.Apply(doc.Blocks[0], "   ");

        Assert.Contains("Bleibt stehen.", doc.ToHtml());
    }

    [Fact]
    public void Parse_IgnoresImageDataUris_SoTheyNeverReachTheModel()
    {
        var doc = TextBlocks.Parse(
            "<p>Text vor dem Bild</p><p><img src=\"data:image/png;base64,AAAABBBBCCCC\"></p>");

        Assert.DoesNotContain(doc.Blocks, b => b.Text.Contains("base64"));
        Assert.DoesNotContain("base64", doc.ToPrompt());
    }
}
