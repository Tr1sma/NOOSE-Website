using NOOSE_Website.Models.Handbook;

namespace NOOSE_Website.Services.Handbook;

/// <summary>The glossary prepared for matching: every term and synonym, longest first, bucketed by first letter.</summary>
/// <remarks>
/// Built once and cached, because it is asked the same question by every rich-text block on a page. Longest-first
/// is not a nicety: the glossary ships "Agent", "Junior Agent" and "Supervisory Special Agent", and matching the
/// short one first would leave "Supervisory Special <em>Agent</em>".
/// <para>
/// Bucketing by first letter keeps the scan linear in practice - roughly ten candidates per position instead of
/// two hundred - which matters because the walk runs over every text node of every document on screen.
/// </para>
/// </remarks>
public sealed class GlossaryMatcher
{
    /// <param name="Phrase">The term or one of its synonyms.</param>
    /// <param name="TermId">Identity of the term, so the same term is not bubbled twice under two spellings.</param>
    public sealed record Entry(string Phrase, string TermId, string Term, string Definition);

    public static readonly GlossaryMatcher Empty = new(new Dictionary<char, List<Entry>>());

    private readonly Dictionary<char, List<Entry>> _byFirstLetter;

    private GlossaryMatcher(Dictionary<char, List<Entry>> byFirstLetter) => _byFirstLetter = byFirstLetter;

    public bool IsEmpty => _byFirstLetter.Count == 0;

    public static GlossaryMatcher Build(IEnumerable<GlossaryTermView> terms)
    {
        var entries = new List<Entry>();
        foreach (var term in terms)
        {
            if (string.IsNullOrWhiteSpace(term.Term) || string.IsNullOrWhiteSpace(term.ShortDefinition))
            {
                continue;
            }
            Add(entries, term.Term, term);
            // synonyms arrive as one comma-separated string; nothing else in the codebase splits it
            foreach (var synonym in (term.Synonyms ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                Add(entries, synonym, term);
            }
        }

        if (entries.Count == 0)
        {
            return Empty;
        }

        var buckets = entries
            .GroupBy(e => char.ToLowerInvariant(e.Phrase[0]))
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(e => e.Phrase.Length)
                      .ThenBy(e => e.Phrase, StringComparer.Ordinal)
                      .ToList());
        return new GlossaryMatcher(buckets);
    }

    private static void Add(List<Entry> entries, string? phrase, GlossaryTermView term)
    {
        var trimmed = phrase?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return;
        }
        entries.Add(new Entry(trimmed, term.Id, term.Term, term.ShortDefinition));
    }

    /// <summary>The longest phrase starting exactly at <paramref name="index"/>, or null.</summary>
    /// <remarks>
    /// Both ends must sit on a word boundary. German compounds are the reason: without it "Fahndung" lights up
    /// inside "Fahndungsliste" and "Agent" inside "Agententätigkeit". A term split across inline tags
    /// (<c>Ver&lt;b&gt;schluss&lt;/b&gt;sache</c>) is missed on purpose - matching across nodes would mean
    /// re-cutting the markup.
    /// </remarks>
    public Entry? LongestAt(string text, int index)
    {
        if (index > 0 && IsWordCharacter(text[index - 1]))
        {
            return null;
        }
        if (!_byFirstLetter.TryGetValue(char.ToLowerInvariant(text[index]), out var candidates))
        {
            return null;
        }

        foreach (var entry in candidates)
        {
            var end = index + entry.Phrase.Length;
            if (end > text.Length)
            {
                continue;
            }
            if (string.Compare(text, index, entry.Phrase, 0, entry.Phrase.Length,
                    StringComparison.OrdinalIgnoreCase) != 0)
            {
                continue;
            }
            if (end < text.Length && IsWordCharacter(text[end]))
            {
                continue;
            }
            // the bucket is sorted longest first, so the first hit is the longest
            return entry;
        }
        return null;
    }

    // letters and digits only: a hyphen has to count as a boundary, or "Nur-Lese-Aufsicht" could never match
    private static bool IsWordCharacter(char c) => char.IsLetterOrDigit(c);
}
