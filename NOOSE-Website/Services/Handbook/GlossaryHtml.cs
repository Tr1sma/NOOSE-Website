using System.Net;
using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace NOOSE_Website.Services.Handbook;

/// <summary>Marks the first occurrence of each glossary term in rendered HTML so the reader can hover it.</summary>
/// <remarks>
/// Runs on the render path, after <see cref="HtmlCleanup.Clean"/> and after the mention pass, so its output never
/// meets the sanitizer - which allows neither the <c>data-</c> attribute this writes nor a <c>title</c>.
/// <para>
/// Only text nodes are rewritten, never the serialized string: a term like "Fahndung" also occurs in
/// <c>href="/fahndung"</c>, and a regex over the markup would inject a span into the middle of a tag.
/// </para>
/// <para>
/// Four kinds of ancestor are skipped. Links, because a bubble inside a link steals the click; code and pre,
/// because their content is quoted verbatim; and anything already carrying a mention or glossary class, because
/// the mention pass emits <c>&lt;span class="erwaehnung erwaehnung-vs"&gt;Verschlusssache&lt;/span&gt;</c> - which
/// is itself a glossary term, and is a span rather than a link, so the link rule would not catch it.
/// </para>
/// </remarks>
public static class GlossaryHtml
{
    /// <summary>Elements whose content is left alone, by CSS selector.</summary>
    private const string SkipSelector = "a, code, pre, .erwaehnung, .glossar";

    /// <summary>
    /// Wraps the first occurrence of each term. "First" means first in this fragment: a record page renders
    /// several independent blocks, and each is annotated on its own rather than through a shared collector.
    /// </summary>
    public static string Annotate(string? html, GlossaryMatcher matcher)
    {
        if (string.IsNullOrEmpty(html) || matcher.IsEmpty)
        {
            return html ?? string.Empty;
        }

        var body = new HtmlParser().ParseDocument(html).Body;
        if (body is null)
        {
            return html;
        }

        var used = new HashSet<string>(StringComparer.Ordinal);
        var touched = false;

        // materialized before the walk: the rewrite splices nodes into the same tree
        foreach (var node in TextNodes(body))
        {
            if (node.Parent is null)
            {
                continue;
            }
            // the neighbours across inline tags, because a word does not end where a text node does
            var markup = Rewrite(node.Data, matcher, used, Neighbour(node, false), Neighbour(node, true));
            if (markup is null)
            {
                continue;
            }

            var holder = body.Owner!.CreateElement("span");
            holder.InnerHtml = markup;
            while (holder.FirstChild is { } child)
            {
                node.Parent.InsertBefore(child, node);
            }
            node.Parent.RemoveChild(node);
            touched = true;
        }

        // byte-identical passthrough when nothing matched: re-serialising an untouched tree rewrites entities
        // and attribute quoting on every document, which shows up as format drift in stored tables
        return touched ? body.InnerHtml : html;
    }

    /// <summary>Markup for one text node, or null when it holds no first occurrence.</summary>
    private static string? Rewrite(
        string text, GlossaryMatcher matcher, HashSet<string> used, char? before, char? after)
    {
        StringBuilder? sb = null;
        var copied = 0;
        var i = 0;

        while (i < text.Length)
        {
            var match = matcher.LongestAt(text, i, before, after);
            if (match is null)
            {
                i++;
                continue;
            }

            // a term already bubbled stays plain, but the whole phrase is still consumed: stepping into it
            // would let a shorter term match inside a longer one it is part of
            if (!used.Add(match.Entry.TermId))
            {
                i += match.Length;
                continue;
            }

            sb ??= new StringBuilder();
            sb.Append(WebUtility.HtmlEncode(text[copied..i]));
            sb.Append("<span class=\"glossar\" tabindex=\"0\" data-glossar=\"")
              .Append(WebUtility.HtmlEncode(match.Entry.Definition))
              .Append("\">")
              // the text as written, not the term as catalogued: the reader's own wording has to survive
              .Append(WebUtility.HtmlEncode(text.Substring(i, match.Length)))
              .Append("</span>");

            i += match.Length;
            copied = i;
        }

        if (sb is null)
        {
            return null;
        }
        sb.Append(WebUtility.HtmlEncode(text[copied..]));
        return sb.ToString();
    }

    private static List<IText> TextNodes(IElement body)
    {
        var found = new List<IText>();
        Collect(body, found);
        return found;
    }

    private static void Collect(INode node, List<IText> found)
    {
        foreach (var child in node.ChildNodes)
        {
            if (child is IText text)
            {
                if (!string.IsNullOrWhiteSpace(text.Data) && !Skip(text))
                {
                    found.Add(text);
                }
                continue;
            }
            Collect(child, found);
        }
    }

    private static bool Skip(INode node) => node.ParentElement?.Closest(SkipSelector) is not null;

    /// <summary>Elements a word runs through without ending.</summary>
    /// <remarks>
    /// <c>a</c> and <c>code</c> are in here although the pass never annotates inside them: for the boundary test
    /// the question is whether the word continues on screen, not whether that stretch may carry a bubble.
    /// </remarks>
    private static readonly HashSet<string> Inline = new(StringComparer.OrdinalIgnoreCase)
    {
        "span", "b", "strong", "i", "em", "u", "s", "a", "code", "sub", "sup", "mark", "small",
        "abbr", "ins", "del", "q", "cite", "var", "kbd", "samp", "time", "bdi", "bdo", "font",
    };

    /// <summary>The character next to this text in the rendered flow, or null where the word has to end.</summary>
    /// <remarks>
    /// Walks sideways and, while the parent is inline, upwards: <c>&lt;b&gt;Fahndung&lt;/b&gt;sliste</c> keeps the
    /// "s" reachable from inside the bold run. A block, a <c>br</c> or an image ends the word and yields null;
    /// an empty text node or an empty inline element is stepped over rather than treated as an ending.
    /// </remarks>
    private static char? Neighbour(IText node, bool forward)
    {
        INode? current = node;
        while (current is not null)
        {
            var sibling = forward ? current.NextSibling : current.PreviousSibling;
            if (sibling is null)
            {
                var parent = current.ParentElement;
                current = parent is not null && Inline.Contains(parent.LocalName) ? parent : null;
                continue;
            }
            if (sibling is IText text)
            {
                if (text.Data.Length > 0)
                {
                    return forward ? text.Data[0] : text.Data[^1];
                }
                current = sibling;
                continue;
            }
            if (sibling is IElement element && Inline.Contains(element.LocalName))
            {
                if (Edge(element, forward, out var hart) is { } inner)
                {
                    return inner;
                }
                if (hart)
                {
                    // an image or a break inside the inline run ends the word just as one beside it would
                    return null;
                }
                current = sibling;
                continue;
            }
            return null;
        }
        return null;
    }

    /// <summary>The first character this node contributes at its leading (or trailing) edge.</summary>
    /// <param name="hardStop">
    /// True when the search ran into something that ends the word - a block, a break, an image - rather than into
    /// nothing at all. Both used to answer null, so the caller stepped over an image inside an inline run and read
    /// the text behind it: "Fahndung&lt;b&gt;&lt;img&gt;sliste&lt;/b&gt;" lost the bubble it was owed.
    /// </param>
    private static char? Edge(INode node, bool forward, out bool hardStop)
    {
        hardStop = false;
        if (node is IText text)
        {
            return text.Data.Length > 0 ? (forward ? text.Data[0] : text.Data[^1]) : null;
        }
        if (node is not IElement element || !Inline.Contains(element.LocalName))
        {
            hardStop = true;
            return null;
        }
        var children = element.ChildNodes;
        for (var i = 0; i < children.Length; i++)
        {
            if (Edge(children[forward ? i : children.Length - 1 - i], forward, out hardStop) is { } c)
            {
                return c;
            }
            if (hardStop)
            {
                return null;
            }
        }
        hardStop = false;
        return null;
    }
}
