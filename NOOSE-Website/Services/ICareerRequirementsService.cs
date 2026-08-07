using System.Security.Claims;
using NOOSE_Website.Models.Recruiting;

namespace NOOSE_Website.Services;

/// <summary>Requirement list of the public career page, stored as JSON in a single system-setting row.</summary>
public interface ICareerRequirementsService
{
    /// <summary>Cached list for the anonymous career page.</summary>
    Task<CareerRequirementsConfig> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Always-fresh list for the editor, so it never shows a stale cache.</summary>
    Task<CareerRequirementsConfig> GetEditableAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(CareerRequirementsConfig config, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
