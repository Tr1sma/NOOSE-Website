using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace NOOSE_Website.Services;

/// <summary>One correctable text block of a document: its plain text plus the element it came from.</summary>
public sealed record TextBlock(int Number, string Text, IElement Element);

/// <summary>A parsed document, ready to be sent as numbered blocks and written back afterwards.</summary>
public sealed class TextBlockDocument
{
    internal TextBlockDocument(IElement root, IReadOnlyList<TextBlock> blocks)
    {
        Root = root;
        Blocks = blocks;
    }

    internal IElement Root { get; }

    public IReadOnlyList<TextBlock> Blocks { get; }

    public int TotalChars => Blocks.Sum(b => b.Text.Length);

    /// <summary>The prompt payload: one line per block, prefixed with its number.</summary>
    public string ToPrompt()
    {
        var sb = new StringBuilder();
        foreach (var block in Blocks)
        {
            sb.Append('[').Append(block.Number).Append("] ").AppendLine(block.Text);
        }
        return sb.ToString().TrimEnd();
    }

    public string ToHtml() => Root.InnerHtml;
}

/// <summary>Splits WYSIWYG HTML into numbered plain-text blocks and writes corrections back into the text nodes.</summary>
/// <remarks>
/// The load-bearing decision of the correction feature: the model never sees markup. It therefore cannot change
/// paragraphing, lists, tables or inline formatting — that guarantee is mechanical, not a promise in a prompt.
/// Base64 images never reach it either, because only text content is read.
/// </remarks>
public static partial class TextBlocks
{
    /// <summary>Leaf blocks only; a cell containing paragraphs must not be counted twice. Code is excluded on purpose.</summary>
    private const string BlockSelector = "p, h1, h2, h3, li, blockquote, td, th, caption";

    private const string NestedSelector = "p, h1, h2, h3, li, blockquote, td, th, caption, pre";

    [GeneratedRegex(@"^\s*\[(\d+)\]\s?", RegexOptions.Compiled)]
    private static partial Regex PrefixRegex();

    [GeneratedRegex(@"^```[a-zA-Z]*\s*$|^\s*```\s*$", RegexOptions.Compiled | RegexOptions.Multiline)]
    private static partial Regex FenceRegex();

    /// <summary>Parses HTML into numbered blocks. Empty blocks stay in the list so re-application indices line up.</summary>
    public static TextBlockDocument Parse(string? html)
    {
        var parser = new HtmlParser();
        using var document = parser.ParseDocument($"<div id=\"noosei-root\">{html ?? string.Empty}</div>");
        var root = document.QuerySelector("#noosei-root")!;

        var blocks = new List<TextBlock>();
        var number = 0;
        foreach (var element in root.QuerySelectorAll(BlockSelector))
        {
            // a block that itself contains blocks is a container, not a leaf
            if (element.QuerySelector(NestedSelector) is not null)
            {
                continue;
            }
            if (element.Closest("pre") is not null)
            {
                continue;
            }
            blocks.Add(new TextBlock(++number, Normalise(element.TextContent), element));
        }

        // a document with no block elements at all (bare text) is still correctable as one block
        if (blocks.Count == 0 && !string.IsNullOrWhiteSpace(root.TextContent))
        {
            blocks.Add(new TextBlock(1, Normalise(root.TextContent), root));
        }

        return new TextBlockDocument(root, blocks);
    }

    /// <summary>Parses the model's answer back into block number → corrected text. Null when it is unusable.</summary>
    public static IReadOnlyDictionary<int, string>? ParseAnswer(string? answer, int expectedBlocks)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return null;
        }

        var result = new Dictionary<int, string>();
        var current = 0;
        var buffer = new StringBuilder();

        foreach (var raw in FenceRegex().Replace(answer, string.Empty).Split('\n'))
        {
            var match = PrefixRegex().Match(raw);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var number))
            {
                Flush(result, current, buffer);
                current = number;
                buffer.Append(raw[match.Length..]);
                continue;
            }
            if (current == 0)
            {
                continue; // preamble before the first [1]
            }
            // a model that wrapped one block over two lines; keep it as one block
            buffer.Append(' ').Append(raw);
        }
        Flush(result, current, buffer);

        // same count, same indices, no gaps, no duplicates — anything else is not usable
        if (result.Count != expectedBlocks)
        {
            return null;
        }
        for (var i = 1; i <= expectedBlocks; i++)
        {
            if (!result.ContainsKey(i))
            {
                return null;
            }
        }
        return result;
    }

    private static void Flush(Dictionary<int, string> result, int number, StringBuilder buffer)
    {
        if (number > 0 && !result.ContainsKey(number))
        {
            result[number] = Normalise(buffer.ToString());
        }
        buffer.Clear();
    }

    /// <summary>Writes corrected text back into a block, preserving every inline element inside it.</summary>
    public static void Apply(TextBlock block, string corrected)
    {
        if (string.Equals(block.Text, corrected, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(corrected))
        {
            return;
        }

        var textNodes = Descend(block.Element).ToList();
        if (textNodes.Count == 0)
        {
            return;
        }
        if (textNodes.Count == 1)
        {
            textNodes[0].TextContent = corrected;
            return;
        }

        // several text nodes means inline markup inside the block: distribute the corrected words back over the
        // nodes in the original proportions, so <b>festgenomen</b> becomes <b>festgenommen</b> and stays bold
        var originalTotal = textNodes.Sum(n => n.TextContent.Length);
        if (originalTotal == 0)
        {
            textNodes[0].TextContent = corrected;
            return;
        }

        var consumed = 0;
        for (var i = 0; i < textNodes.Count; i++)
        {
            var node = textNodes[i];
            int take;
            if (i == textNodes.Count - 1)
            {
                take = corrected.Length - consumed;
            }
            else
            {
                var share = (double)node.TextContent.Length / originalTotal;
                take = (int)Math.Round(corrected.Length * share);
                take = Math.Clamp(take, 0, corrected.Length - consumed);
                take = ExtendToWordBoundary(corrected, consumed, take);
            }
            node.TextContent = take <= 0 ? string.Empty : corrected.Substring(consumed, take);
            consumed += Math.Max(0, take);
        }
    }

    /// <summary>Nudges a split point to the next space, so a correction never cuts a word in half across two nodes.</summary>
    private static int ExtendToWordBoundary(string text, int start, int take)
    {
        var end = start + take;
        if (end <= start || end >= text.Length)
        {
            return take;
        }
        var probe = end;
        while (probe < text.Length && !char.IsWhiteSpace(text[probe]))
        {
            probe++;
        }
        return probe - start;
    }

    private static IEnumerable<INode> Descend(INode node)
    {
        foreach (var child in node.ChildNodes)
        {
            if (child.NodeType == NodeType.Text)
            {
                if (!string.IsNullOrEmpty(child.TextContent))
                {
                    yield return child;
                }
                continue;
            }
            foreach (var nested in Descend(child))
            {
                yield return nested;
            }
        }
    }

    private static string Normalise(string? text)
        => string.IsNullOrWhiteSpace(text) ? string.Empty : Regex.Replace(text, @"\s+", " ").Trim();
}
