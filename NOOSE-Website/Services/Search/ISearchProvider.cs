using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services.Search;

/// <summary>How a partner may reach a category at all.</summary>
public enum PartnerAccess
{
    /// <summary>Internal only. The overwhelming majority.</summary>
    Never,

    /// <summary>Released to the partner directly; exactly the types <see cref="PartnerVisibility.IsReleasableType"/> allows.</summary>
    ViaShare,

    /// <summary>Reached through a released parent. Capped transitively, because the parent check rejects a
    /// non-releasable parent type before anything else.</summary>
    ViaParentShare,
}

/// <summary>One searchable category.</summary>
/// <remarks>
/// Each provider owns its own <c>IDbContextFactory</c>: the orchestrator runs several at once, and a shared context
/// throws "A second operation was started on this context" on the first concurrent pair.
/// A provider never writes a visibility predicate — it names one. Anything else is a second copy of a rule that
/// already exists somewhere, and the copies drift.
/// </remarks>
public interface ISearchProvider
{
    /// <summary>CLR name of the <see cref="SearchCatalog"/> row this provider fills.</summary>
    string Category { get; }

    /// <summary>Structural partner behaviour. Not a runtime decision.</summary>
    PartnerAccess Partner { get; }

    /// <summary>Whether this viewer may search this category at all.</summary>
    /// <remarks>Pure and DB-free: it decides without paying a round trip. False removes the category from the
    /// viewer's catalog entirely — no facet, no group, no zero. "You may search here and nothing matched" and
    /// "this category is not yours" must not look the same.</remarks>
    bool AppliesTo(SearchViewer viewer);

    Task<IReadOnlyList<SearchHit>> SearchAsync(SearchQuery query, CancellationToken cancellationToken = default);

    /// <summary>Resolves phonetic/stem side-index candidate ids against the live, gated table.</summary>
    /// <remarks>Only <see cref="SearchTraits.SideIndexed"/> categories override this — and they must, because the
    /// alternative is the side-index pass keeping its own copy of every visibility rule, which is how the old one
    /// came to ignore both the partner filter and the tag filter.</remarks>
    Task<IReadOnlyList<SearchHit>> ResolveIdsAsync(
        SearchQuery query, IReadOnlyCollection<string> ids, int take, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SearchHit>>([]);

    /// <summary>Compact hits for the command palette. Only <see cref="SearchTraits.Quick"/> categories override this.</summary>
    Task<IReadOnlyList<QuickHit>> QuickAsync(
        SearchQuery query, int max, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<QuickHit>>([]);
}
