using System.Net;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Ganss.Xss;

namespace NOOSE_Website.Services;

/// <summary>Server-side HTML sanitizer for WYSIWYG content.</summary>
public static partial class HtmlCleanup
{
    /// <summary>Marker the editor leaves behind where an image was, while NOOSEI works on the text.</summary>
    public const string AiImagePlaceholderAttribute = "data-noosei-bild";

    /// <summary>Sanitizes HTML; never returns null.</summary>
    public static string Clean(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }
        return Generate().Sanitize(html);
    }

    /// <summary>Sanitizes NOOSEI diff markup: the same allowlist plus the ins/del marks the diff renderer adds.</summary>
    public static string CleanDiff(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }
        return Generate(allowDiffMarks: true).Sanitize(html);
    }

    /// <summary>Sanitizes editor HTML on its way to NOOSEI, keeping the image placeholder.</summary>
    /// <remarks>
    /// The editor swaps every base64 image for <c>data-noosei-bild="n"</c> before marshalling and puts the
    /// picture back on apply. A placeholder in <c>src</c> cannot work: it is a URI attribute, and any scheme
    /// outside the list below is dropped here — which silently deleted the image from the corrected document.
    /// </remarks>
    public static string CleanAiPayload(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }
        return Generate(allowImagePlaceholder: true).Sanitize(html);
    }

    /// <summary>Tags out, entities decoded, whitespace collapsed to single spaces. For search snippets, Discord
    /// embeds, LLM context and emptiness probes.</summary>
    /// <remarks>
    /// NOT a sanitizer — the result is meant to be rendered as TEXT; use <see cref="Clean"/> for anything that
    /// stays markup. Regex rather than a <see cref="Clean"/>-then-strip round trip because that is a full AngleSharp
    /// parse, and the search path runs this over dozens of rows per category.
    /// Block structure is deliberately lost: a caller that needs paragraph breaks (the applicant letter) keeps its
    /// own converter, because collapsing its newlines would run the whole letter together.
    /// </remarks>
    public static string PlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }
        // space, not empty: stripping <b>a</b><b>b</b> to "ab" glues neighbouring words together
        var text = WebUtility.HtmlDecode(TagStrip().Replace(html, " "));
        return Whitespace().Replace(text, " ").Trim();
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagStrip();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    private static HtmlSanitizer Generate(bool allowDiffMarks = false, bool allowImagePlaceholder = false)
    {
        var s = new HtmlSanitizer();

        s.AllowedTags.Clear();
        foreach (var tag in new[]
        {
            "p", "br", "span", "b", "strong", "i", "em", "u", "s",
            "h1", "h2", "h3", "ul", "ol", "li", "blockquote", "pre", "code", "a", "img",
            "table", "thead", "tbody", "tr", "td", "th", "caption", "colgroup", "col", "div", "contain",
        })
        {
            s.AllowedTags.Add(tag);
        }
        if (allowDiffMarks)
        {
            s.AllowedTags.Add("ins");
            s.AllowedTags.Add("del");
        }

        s.AllowedAttributes.Clear();
        foreach (var attr in new[]
        {
            "href", "target", "rel", "class", "style", "src", "alt",
            "colspan", "rowspan", "width", "cellpadding", "cellspacing", "contenteditable",
            "data-table-id", "data-row-id", "data-col-id", "data-rowspan", "data-colspan",
            "data-row", "data-col", "data-w", "data-full",
        })
        {
            s.AllowedAttributes.Add(attr);
        }
        if (allowImagePlaceholder)
        {
            s.AllowedAttributes.Add(AiImagePlaceholderAttribute);
        }

        s.AllowedCssProperties.Clear();
        foreach (var prop in new[]
        {
            "color", "background-color", "text-align", "font-size",
            "width", "height", "vertical-align",
            "border", "border-color", "border-style", "border-width",
        })
        {
            s.AllowedCssProperties.Add(prop);
        }

        s.AllowedSchemes.Clear();
        s.AllowedSchemes.Add("http");
        s.AllowedSchemes.Add("https");
        s.AllowedSchemes.Add("mailto");
        // quill embeds pasted/picked images as base64 data URIs
        s.AllowedSchemes.Add("data");

        // data: stays image-only; a data: href is a phishing vector
        s.PostProcessNode += (_, e) =>
        {
            if (e.Node is IElement { NodeName: "A" } anchor
                && anchor.GetAttribute("href")?.StartsWith("data:", StringComparison.OrdinalIgnoreCase) == true)
            {
                anchor.RemoveAttribute("href");
            }
        };

        return s;
    }
}
