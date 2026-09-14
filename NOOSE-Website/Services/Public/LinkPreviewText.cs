using System.Text.RegularExpressions;

namespace NOOSE_Website.Services.Public;

/// <summary>The two short strings a link preview carries, taken from what the page already says.</summary>
/// <remarks>
/// An unfurler - Discord above all - reads one title, one sentence and one picture out of the head and renders a
/// card from them. It cuts an over-long sentence mid-word and without a mark, so the trimming happens here instead:
/// on a word boundary, with an ellipsis, so the card still reads as a sentence.
/// <para>
/// Static because it is text arithmetic with no state, like <see cref="PublicRoutes"/> - and because the component
/// that uses it is a <c>.razor</c> file, which no test in this project can render.
/// </para>
/// </remarks>
public static partial class LinkPreviewText
{
    /// <summary>Characters a description keeps.</summary>
    /// <remarks>Discord shows roughly 300 and then stops; 200 leaves room for the title above it.</remarks>
    public const int MaxDescription = 200;

    /// <summary>Characters of markup looked at when a description is taken from a body.</summary>
    /// <remarks>
    /// A press release or a report carries its pictures as base64 inside the body, so one document can be megabytes.
    /// Stripping all of it on every anonymous request to keep two hundred characters would be absurd, and the first
    /// paragraph is the summary anyway.
    /// </remarks>
    private const int ScanWindow = 4000;

    /// <summary>One plain sentence for the preview card, or empty.</summary>
    public static string Summary(string? html, int max = MaxDescription)
        => string.IsNullOrWhiteSpace(html)
            ? string.Empty
            : Clip(Dangling().Replace(HtmlCleanup.PlainText(Window(html)), "$1"), max);

    /// <summary>Whitespace in front of a punctuation mark, which the tag stripper leaves behind.</summary>
    /// <remarks>
    /// It puts a space where every tag was - deliberately, so that <c>&lt;b&gt;a&lt;/b&gt;&lt;b&gt;b&lt;/b&gt;</c>
    /// does not become one word - and a sentence ending in <c>&lt;/b&gt;.</c> therefore arrives as "Raubes .".
    /// Harmless in a search index, visible in a card.
    /// </remarks>
    [GeneratedRegex(@"\s+([.,;:!?])")]
    private static partial Regex Dangling();

    /// <summary>Cut to length on a word boundary, with an ellipsis when something was dropped.</summary>
    public static string Clip(string? text, int max = MaxDescription)
    {
        var value = (text ?? string.Empty).Trim();
        if (max <= 0 || value.Length <= max)
        {
            return value;
        }
        var cut = value[..max];
        var space = cut.LastIndexOf(' ');
        // a single word longer than the whole budget has no boundary to cut on, so it is cut hard
        var kept = space > max / 2 ? cut[..space] : cut;
        return kept.TrimEnd(' ', ',', ';', ':', '.', '-', '\u2013', '\u2014') + "\u2026";
    }

    /// <summary>The address without query and fragment: one canonical URL per page.</summary>
    /// <remarks>
    /// A filter chip and an anchor produce a different address for the same page, and announcing each of them as its
    /// own canonical URL turns one page into many for everything that remembers them.
    /// </remarks>
    public static string Canonical(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return string.Empty;
        }
        var stop = uri.AsSpan().IndexOfAny('?', '#');
        return stop < 0 ? uri : uri[..stop];
    }

    /// <summary>The leading markup, never cut inside a tag.</summary>
    /// <remarks>
    /// Cutting mid-tag would leave an <c>&lt;img src="data:image/png;base64,iVBOR...</c> without its closing bracket,
    /// and the tag stripper would then hand that base64 straight into the card.
    /// </remarks>
    private static string Window(string html)
    {
        if (html.Length <= ScanWindow)
        {
            return html;
        }
        var end = html.LastIndexOf('>', ScanWindow - 1);
        return end < 0 ? string.Empty : html[..(end + 1)];
    }
}
