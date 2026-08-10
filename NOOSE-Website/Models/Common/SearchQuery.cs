using NOOSE_Website.Services;
using NOOSE_Website.Services.Search;

namespace NOOSE_Website.Models.Common;

/// <summary>One search, normalised once and handed to every provider unchanged.</summary>
/// <remarks>The tokenisation, the caps and the viewer cannot then diverge between forty implementations, and the
/// query tokens are computed once instead of once per result group.</remarks>
public sealed record SearchQuery
{
    /// <summary>Trimmed search text. Empty means "browse", not "match nothing".</summary>
    public required string Text { get; init; }

    public bool HasText => Text.Length > 0;

    /// <summary>Normalised words of <see cref="Text"/>, for the Levenshtein pass and the relevance score.</summary>
    public IReadOnlyList<string> Tokens { get; init; } = [];

    /// <summary>List, not IReadOnlyList: EF's IN translation wants a concrete collection parameter.</summary>
    public List<string> TagIds { get; init; } = [];

    public bool HasTags => TagIds.Count > 0;

    /// <summary>Typo tolerance via in-memory Levenshtein on top of the exact search.</summary>
    public bool Fuzzy { get; init; }

    /// <summary>Deep scan: also match a record's side fields.</summary>
    public bool Deep { get; init; }

    /// <summary>Categories the caller restricted the search to. Empty = every provider the viewer may use.</summary>
    /// <remarks>The search page never fills this — there a category is a filter on the result, not on the query.
    /// The assistant does, and so does the "load the rest" retry after an expired budget.</remarks>
    public IReadOnlyList<string> OnlyCategories { get; init; } = [];

    public required SearchViewer Viewer { get; init; }

    public ViewerScope Scope => Viewer.Scope;

    public int PerCategory { get; init; } = 50;

    public int FuzzyCandidates { get; init; } = 2_000;

    /// <summary>Whether a fuzzy supplement is still worth the candidate scan.</summary>
    public bool WantsFuzzy(int found) => Fuzzy && HasText && found < PerCategory;

    public static SearchQuery From(SearchCriteria criteria, SearchViewer viewer, SearchOptions options)
    {
        var text = criteria.Text?.Trim() ?? string.Empty;
        return new SearchQuery
        {
            Text = text,
            Tokens = text.Length > 0 ? TextSimilarity.Tokens(text) : [],
            TagIds = criteria.TagIds ?? [],
            Fuzzy = criteria.Fuzzy,
            Deep = criteria.MaxMode,
            OnlyCategories = criteria.Categories ?? [],
            Viewer = viewer,
            PerCategory = options.PerCategory,
            FuzzyCandidates = options.FuzzyCandidates,
        };
    }
}
