using System.Security.Claims;
using NOOSE_Website.Data.Entities.Activities;
using NOOSE_Website.Models.Activities;

namespace NOOSE_Website.Services;

/// <summary>Personal agent activities: everyone reads, only creator or leadership writes. Not classified.</summary>
public interface IAgentActivityService
{
    /// <summary>All activities (newest first) with owner name and visible org links.</summary>
    Task<List<AgentActivityListItem>> GetListAsync(ViewerScope scope, CancellationToken cancellationToken = default);

    /// <summary>A single activity with resolved owner name and visible org links, or null if missing.</summary>
    Task<AgentActivityDetailView?> GetDetailAsync(string id, ViewerScope scope, CancellationToken cancellationToken = default);

    /// <summary>Activities linked to a faction / person group (for the org detail tab).</summary>
    Task<List<AgentActivityListItem>> GetLinkedAsync(string targetType, string targetId, ViewerScope scope, CancellationToken cancellationToken = default);

    /// <summary>Full linked activities (incl. HTML body) for the print dossier.</summary>
    Task<List<AgentActivity>> GetLinkedFullAsync(string targetType, string targetId, CancellationToken cancellationToken = default);

    /// <summary>Soft-deleted activities for the trash (leadership page).</summary>
    Task<List<AgentActivityListItem>> GetTrashAsync(CancellationToken cancellationToken = default);

    /// <summary>Distinct non-empty kinds for autocomplete suggestions.</summary>
    Task<List<string>> GetKindsAsync(CancellationToken cancellationToken = default);

    Task<AgentActivity> CreateAsync(AgentActivityInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task UpdateAsync(string id, AgentActivityInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
