using System.Globalization;
using System.Text;

namespace NOOSE_Website.Services.Public;

/// <summary>Limits of the public search. The service, the page and the tests all read them here.</summary>
public static class PublicSearchRules
{
    /// <summary>Below this a query is not a search; it is a dump request.</summary>
    /// <remarks>
    /// One or two letters would match nearly every published row, which makes the endpoint a scraper rather than a
    /// search. The page says the minimum out loud instead of answering "nothing found", which would be a lie.
    /// </remarks>
    public const int MinQueryLength = 3;

    /// <summary>Bounds the in-memory tokenisation an anonymous visitor can trigger.</summary>
    public const int MaxQueryLength = 100;

    /// <summary>Hits shown per published surface.</summary>
    public const int PerAreaLimit = 10;

    /// <summary>Characters kept on each side of the match in a snippet.</summary>
    public const int SnippetRadius = 110;

    /// <summary>Trims a query to what the search will actually run.</summary>
    /// <remarks>
    /// Weightless code points — control and format characters, zero-width joiners and the like — are dropped BEFORE
    /// the length gate. A culture-sensitive comparison treats a string made only of those as equal at position zero,
    /// so three zero-width spaces would pass the minimum and then match every published row: the whole corpus,
    /// handed to an anonymous visitor as a search result.
    /// <para>
    /// The category list alone is not enough, and a longer list would not fix it either: a variation selector
    /// (U+FE0F) and the combining grapheme joiner (U+034F) are NonSpacingMark, survive any such filter, and still
    /// carry zero collation weight. So what remains is measured against the empty string instead — that asks the
    /// comparer the same question the matcher will ask, whatever the code point.
    /// </para>
    /// </remarks>
    public static string Normalise(string? query)
    {
        var raw = query ?? string.Empty;
        var kept = new StringBuilder(raw.Length);
        foreach (var c in raw)
        {
            if (!char.IsControl(c) && char.GetUnicodeCategory(c) is not (UnicodeCategory.Format
                or UnicodeCategory.OtherNotAssigned or UnicodeCategory.Surrogate
                or UnicodeCategory.PrivateUse))
            {
                kept.Append(c);
            }
        }

        var text = kept.ToString().Trim();
        if (text.Length > MaxQueryLength)
        {
            text = text[..MaxQueryLength];
        }
        // nothing the comparer can weigh is not a search term: it would match at position zero of every row
        return HasCollationWeight(text) ? text : string.Empty;
    }

    /// <summary>True when the comparer sees anything at all in the text; the matcher uses the same comparison.</summary>
    private static bool HasCollationWeight(string text)
        => text.Length > 0
            && CultureInfo.CurrentCulture.CompareInfo.Compare(
                text, string.Empty, CompareOptions.IgnoreCase) != 0;

    public static bool IsTooShort(string normalised) => normalised.Length < MinQueryLength;
}
