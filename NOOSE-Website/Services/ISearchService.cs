using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <summary>Global full-text search across all record types.</summary>
public interface ISearchService
{
    // taskforce visibility
    Task<List<SearchResultGroup>> SearchAsync(SearchCriteria criteria, bool isLeadership, string? meId, CancellationToken cancellationToken = default);

    /// <summary>Quick person lookup for command palette.</summary>
    Task<List<QuickHit>> QuickSearchAsync(string text, bool isLeadership, string? meId, int max = 8, CancellationToken cancellationToken = default);
}
