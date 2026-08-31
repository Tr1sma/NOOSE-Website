namespace NOOSE_Website.Models.Public;

/// <summary>One released paragraph as the public reads it.</summary>
/// <remarks>
/// The law text is plain text, not markup — line breaks are its formatting, so the page renders it as such rather than
/// as HTML. Carries no id, no author and no timestamps: a statute is the text, and the rest is bookkeeping.
/// </remarks>
public sealed record PublicLawEntry(string Paragraph, string Title, string Text, string? Sentence);

/// <summary>The released paragraphs of one law book, in reading order.</summary>
public sealed record PublicLawBook(string Name, IReadOnlyList<PublicLawEntry> Entries);

/// <summary>Everything the public law page reads, cached as one unit.</summary>
public sealed record PublicLawSnapshot(IReadOnlyList<PublicLawBook> Books)
{
    public static PublicLawSnapshot Empty { get; } = new([]);
}

/// <summary>One paragraph in the release panel: what it is, and whether it is out.</summary>
public sealed record LawReleaseRow(string Id, string LawBook, string Paragraph, string Title, bool IsPublic);
