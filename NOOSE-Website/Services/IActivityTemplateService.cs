using System.Security.Claims;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <summary>Management of activity templates (HTML body); writes are leadership-only.</summary>
public interface IActivityTemplateService
{
    /// <summary>All templates for management, sorted.</summary>
    Task<List<ActivityTemplate>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Active templates only for the create-activity picker.</summary>
    Task<List<ActivityTemplate>> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>A single template with HTML body, or null if missing.</summary>
    Task<ActivityTemplate?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task<ActivityTemplate> CreateAsync(ActivityTemplateInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task RefreshAsync(string id, ActivityTemplateInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
