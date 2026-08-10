using System.Security.Claims;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <summary>Global search across every category the viewer may see.</summary>
/// <remarks>Takes the principal rather than a <see cref="ViewerScope"/>: three of the canonical gates are
/// principal-shaped, and the partner rank allowlist has to be resolved per request.</remarks>
public interface ISearchService
{
    Task<SearchResults> SearchAsync(SearchCriteria criteria, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Identifier-only lookup for the command palette, under a much tighter budget.</summary>
    /// <remarks>Returns a bare list, not an envelope: a palette that offers fewer suggestions while you keep
    /// typing is not the same lie as a results page claiming "keine Treffer".</remarks>
    Task<List<QuickHit>> QuickSearchAsync(string text, ClaimsPrincipal actor, int max = 8, CancellationToken cancellationToken = default);
}
