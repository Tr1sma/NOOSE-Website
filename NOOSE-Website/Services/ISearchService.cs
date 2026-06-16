using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <summary>Global full-text search across all record types.</summary>
public interface ISearchService
{
    // partners see only released, non-classified records
    Task<List<SearchResultGroup>> SearchAsync(SearchCriteria criteria, ViewerScope scope, CancellationToken cancellationToken = default);

    /// <summary>Quick person lookup for command palette.</summary>
    Task<List<QuickHit>> QuickSearchAsync(string text, ViewerScope scope, int max = 8, CancellationToken cancellationToken = default);
}
