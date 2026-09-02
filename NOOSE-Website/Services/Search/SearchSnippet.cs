namespace NOOSE_Website.Services.Search;

/// <summary>Builds the bit of text a result row shows under the title.</summary>
public static class SearchSnippet
{
    /// <summary>Characters kept on each side of the match.</summary>
    public const int Radius = 90;

    /// <summary>Hard ceiling for a snippet without a match to centre on.</summary>
    public const int HeadMax = 200;

    /// <summary>Plain-text window around the first occurrence of the query, with ellipsis markers.</summary>
    /// <remarks>The head of a 40 KB document is almost never the reason it matched, so a snippet that just takes the
    /// first 160 characters reads as a wrong hit. Falls back to the head when the term is not in the plain projection
    /// — it may have sat inside an attribute, which <see cref="HtmlCleanup.PlainText"/> drops.</remarks>
    public static string Around(string? plain, string? query, int radius = Radius)
    {
        if (string.IsNullOrEmpty(plain))
        {
            return string.Empty;
        }
        var at = string.IsNullOrWhiteSpace(query)
            ? -1
            : plain.IndexOf(query.Trim(), StringComparison.CurrentCultureIgnoreCase);
        if (at < 0)
        {
            return plain.Length <= HeadMax ? plain : plain[..Back(plain, HeadMax)].TrimEnd() + "…";
        }

        var from = Back(plain, Math.Max(0, at - radius));
        var to = Back(plain, Math.Min(plain.Length, at + query!.Trim().Length + radius));
        var window = plain[from..to].Trim();
        return (from > 0 ? "…" : string.Empty) + window + (to < plain.Length ? "…" : string.Empty);
    }

    /// <summary>Moves a cut off the middle of a surrogate pair, always backwards.</summary>
    /// <remarks>
    /// Both cuts are UTF-16 indices, and the haystack comes from the database, so it can hold non-BMP characters:
    /// slicing between the two halves of a pair emits a replacement character. Backwards rather than forwards, so
    /// a snippet never grows past the length its callers assert on.
    /// </remarks>
    private static int Back(string text, int index)
        => index > 0 && index < text.Length && char.IsLowSurrogate(text[index]) ? index - 1 : index;
}
