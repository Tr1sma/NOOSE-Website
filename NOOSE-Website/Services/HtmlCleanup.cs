using AngleSharp.Dom;
using Ganss.Xss;

namespace NOOSE_Website.Services;

/// <summary>Server-side HTML sanitizer for WYSIWYG content.</summary>
public static class HtmlCleanup
{
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

    private static HtmlSanitizer Generate(bool allowDiffMarks = false)
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
