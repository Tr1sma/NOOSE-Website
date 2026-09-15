using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace NOOSE_Website.Services;

/// <summary>Bridges the stored figure markup and the editor's flat image line plus caption line.</summary>
/// <remarks>
/// The editor keeps a caption as an ordinary paragraph so it stays typeable in place; the stored shape is a real
/// figure/figcaption. Both directions live here and are tested, because a lossy round trip would silently eat a
/// caption. A caption paragraph on a carrier that never reaches the wrap step still renders through the same CSS.
/// </remarks>
public static class RichTextFigure
{
    /// <summary>Marker class of the caption line while it is still being edited.</summary>
    public const string CaptionClass = "noose-bildtext";

    /// <summary>Stored html to editor html: every figure becomes an image line plus a caption line.</summary>
    public static string ToEditor(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html ?? string.Empty;
        }
        var document = Parse(html);
        var figures = document.QuerySelectorAll("figure").OfType<IElement>().ToList();
        if (figures.Count == 0)
        {
            return html; // untouched: the common case must not be re-serialized
        }
        foreach (var figure in figures)
        {
            if (figure.QuerySelector("img") is not { } image)
            {
                continue;
            }
            var line = document.CreateElement("p");
            CopyAlignment(figure, line);
            image.Remove();
            line.AppendChild(image);
            var caption = figure.QuerySelector("figcaption");
            figure.Replace(line);
            if (caption is null)
            {
                continue;
            }
            var captionLine = document.CreateElement("p");
            captionLine.ClassList.Add(CaptionClass);
            captionLine.TextContent = caption.TextContent;
            line.After(captionLine);
        }
        return Serialize(document);
    }

    /// <summary>Editor html to stored html: an image line with a caption line below becomes a figure.</summary>
    public static string ToStored(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html ?? string.Empty;
        }
        var document = Parse(html);
        var changed = false;
        foreach (var line in document.QuerySelectorAll("p").OfType<IElement>().ToList())
        {
            // exactly one element and it is the image: folding a line that carries more would drop the rest with
            // the paragraph it replaces. TextContent cannot catch that - an <img> contributes no text - so a
            // second picture pasted into the same line used to vanish on save, before it was ever written to a file.
            if (line.ChildElementCount != 1
                || line.FirstElementChild is not { } image
                || !image.NodeName.Equals("IMG", StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrWhiteSpace(line.TextContent))
            {
                continue;
            }
            if (line.NextElementSibling is not { } next || !next.ClassList.Contains(CaptionClass))
            {
                continue;
            }
            var figure = document.CreateElement("figure");
            CopyAlignment(line, figure);
            image.Remove();
            figure.AppendChild(image);
            var caption = document.CreateElement("figcaption");
            caption.TextContent = next.TextContent;
            figure.AppendChild(caption);
            line.Replace(figure);
            next.Remove();
            changed = true;
        }
        return changed ? Serialize(document) : html;
    }

    private static void CopyAlignment(IElement from, IElement to)
    {
        foreach (var klasse in from.ClassList.Where(k => k.StartsWith("ql-align-", StringComparison.Ordinal)))
        {
            to.ClassList.Add(klasse);
        }
    }

    private static AngleSharp.Html.Dom.IHtmlDocument Parse(string html) => new HtmlParser().ParseDocument(html);

    private static string Serialize(AngleSharp.Html.Dom.IHtmlDocument document)
        => document.Body?.InnerHtml ?? string.Empty;
}
