using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace NOOSE_Website.Services;

/// <summary>Heading entries a table of contents is built from; collected in the editor, keyed by slug.</summary>
public sealed record TocEntry(int Level, string Text);

/// <summary>Anchors for long documents: headings get stable ids, the table of contents links to them.</summary>
/// <remarks>
/// Both halves use the same slug rule, because a TOC link that misses its heading is a silent no-op. Ids are
/// assigned on save (idempotent, duplicate-free) instead of in the browser, so stored documents and the index
/// agree without a second implementation of the rule.
/// </remarks>
public static partial class RichTextAnchors
{
    /// <summary>Class of the table-of-contents list.</summary>
    public const string TocClass = "noose-inhaltsverzeichnis";

    /// <summary>Assigns missing heading ids; returns the same string when there is nothing to do.</summary>
    public static string ToStored(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html ?? string.Empty;
        }
        var document = Parse(html);
        var headings = document.QuerySelectorAll("h1, h2, h3").OfType<IElement>().ToList();
        if (headings.Count == 0)
        {
            return html;
        }
        // An id that this rule could have produced is kept, so saving twice does not renumber the anchors and
        // every link written against them survives. Anything else is dropped and replaced: the sanitizer lets
        // an id through on a heading, so a pasted or hand-written one would otherwise keep whatever name it
        // brought - and an id is a document-wide name that can shadow one the page itself uses.
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var heading in headings)
        {
            if (IsOwnSlug(heading.Id))
            {
                used.Add(heading.Id);
            }
            else if (!string.IsNullOrEmpty(heading.Id))
            {
                heading.RemoveAttribute("id");
            }
        }
        var changed = false;
        foreach (var heading in headings)
        {
            if (!string.IsNullOrWhiteSpace(heading.Id))
            {
                continue;
            }
            var slug = Unique(Slug(heading.TextContent), used);
            heading.Id = slug;
            used.Add(slug);
            changed = true;
        }
        // an id was dropped even if none was added, so compare against the input rather than trusting the flag
        var ausgabe = Serialize(document);
        return changed || !string.Equals(ausgabe, html, StringComparison.Ordinal) ? ausgabe : html;
    }

    /// <summary>Whether an id looks like one this class would have written.</summary>
    private static bool IsOwnSlug(string? id)
        => !string.IsNullOrEmpty(id)
        && id.All(c => (c >= 'a' && c <= 'z') || char.IsAsciiDigit(c) || c == '-')
        && !id.StartsWith('-')
        && !id.EndsWith('-');

    /// <summary>Builds the list markup the editor inserts at the caret.</summary>
    /// <remarks>The level belongs on the item, not on the wrapper: Quill rebuilds the surrounding list from
    /// its own format and drops any class the wrapper carried, so only the item class reaches the document.</remarks>
    public static string BuildToc(IEnumerable<TocEntry> entries)
    {
        var builder = new StringBuilder();
        builder.Append("<ul class=\"").Append(TocClass).Append("\">");
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            var text = (entry.Text ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                continue;
            }
            var slug = Unique(Slug(text), used);
            used.Add(slug);
            builder.Append("<li class=\"noose-toc-").Append(Math.Clamp(entry.Level, 1, 3)).Append("\">")
                .Append("<a href=\"#").Append(slug).Append("\">")
                .Append(System.Net.WebUtility.HtmlEncode(text))
                .Append("</a></li>");
        }
        builder.Append("</ul>");
        return builder.ToString();
    }

    /// <summary>Url-clean anchor of a heading text; the same rule the editor sends its entries through.</summary>
    public static string Slug(string? text)
    {
        var lowered = (text ?? string.Empty).ToLowerInvariant()
            .Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss");
        var cleaned = SlugPattern().Replace(lowered, "-").Trim('-');
        return cleaned.Length == 0 ? "abschnitt" : cleaned;
    }

    private static string Unique(string slug, HashSet<string> used)
    {
        if (!used.Contains(slug))
        {
            return slug;
        }
        for (var nummer = 2; ; nummer++)
        {
            var kandidat = $"{slug}-{nummer}";
            if (!used.Contains(kandidat))
            {
                return kandidat;
            }
        }
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex SlugPattern();

    private static AngleSharp.Html.Dom.IHtmlDocument Parse(string html) => new HtmlParser().ParseDocument(html);

    private static string Serialize(AngleSharp.Html.Dom.IHtmlDocument document)
        => document.Body?.InnerHtml ?? string.Empty;
}
