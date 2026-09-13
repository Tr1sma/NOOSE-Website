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
            var markup = Rewrite(node.Data, matcher, used);
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
    private static string? Rewrite(string text, GlossaryMatcher matcher, HashSet<string> used)
    {
        StringBuilder? sb = null;
        var copied = 0;
        var i = 0;

        while (i < text.Length)
        {
            var match = matcher.LongestAt(text, i);
            if (match is null)
            {
                i++;
                continue;
            }

            // a term already bubbled stays plain, but the whole phrase is still consumed: stepping into it
            // would let a shorter term match inside a longer one it is part of
            if (!used.Add(match.TermId))
            {
                i += match.Phrase.Length;
                continue;
            }

            sb ??= new StringBuilder();
            sb.Append(WebUtility.HtmlEncode(text[copied..i]));
            sb.Append("<span class=\"glossar\" tabindex=\"0\" data-glossar=\"")
              .Append(WebUtility.HtmlEncode(match.Definition))
              .Append("\">")
              // the text as written, not the term as catalogued: the reader's own wording has to survive
              .Append(WebUtility.HtmlEncode(text.Substring(i, match.Phrase.Length)))
              .Append("</span>");

            i += match.Phrase.Length;
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
}
