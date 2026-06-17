using System.Security.Claims;
using NOOSE_Website.Models.Graph;

namespace NOOSE_Website.Services;

/// <summary>Suggests related, not-yet-linked person records via shared phone, faction, group, tag and links; excludes already linked and non-visible records.</summary>
public interface ILinkSuggestionService
{
    /// <summary>Returns ranked suggestions for the given record (empty if none).</summary>
    Task<List<LinkSuggestion>> GetSuggestionsAsync(string entityType, string entityId, ClaimsPrincipal viewer, CancellationToken cancellationToken = default);
}
