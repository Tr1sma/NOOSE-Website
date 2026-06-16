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

    // fresh instance
    private static HtmlSanitizer Generate()
    {
        var s = new HtmlSanitizer();

        s.AllowedTags.Clear();
        foreach (var tag in new[]
        {
            "p", "br", "span", "b", "strong", "i", "em", "u", "s",
            "h1", "h2", "h3", "ul", "ol", "li", "blockquote", "pre", "code", "a",
            // table tags
            "table", "thead", "tbody", "tr", "td", "th", "caption", "colgroup", "col", "div", "contain",
        })
        {
            s.AllowedTags.Add(tag);
        }

        s.AllowedAttributes.Clear();
        foreach (var attr in new[]
        {
            "href", "target", "rel", "class", "style",
            // table attributes
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
            "color", "background-color", "text-align",
            // cell layout
            "width", "height", "vertical-align",
            "border", "border-color", "border-style", "border-width",
        })
        {
            s.AllowedCssProperties.Add(prop);
        }

        // safe schemes only
        s.AllowedSchemes.Clear();
        s.AllowedSchemes.Add("http");
        s.AllowedSchemes.Add("https");
        s.AllowedSchemes.Add("mailto");

        return s;
    }
}
