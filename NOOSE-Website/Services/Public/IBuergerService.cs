using System.Security.Claims;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Services.Public;

/// <summary>Citizen accounts of the public area: own profile, and the agency-side roster with blocking.</summary>
public interface IBuergerService
{
    /// <summary>The signed-in citizen's own profile, or null when none exists yet.</summary>
    Task<BuergerProfil?> GetOwnAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates the signed-in citizen's own name; a change is audited.</summary>
    Task<BuergerProfil> SaveOwnAsync(string firstName, string lastName, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>True once the citizen has both names on file; the portal layout gates on this.</summary>
    Task<bool> HasCompleteProfileAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Guard for every citizen write path: a complete profile and no block. Throws otherwise.</summary>
    Task<BuergerProfil> RequireSubmittingCitizenAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Citizen roster for the agency, newest first; optional free-text filter over name and Discord handle.</summary>
    Task<IReadOnlyList<CitizenRow>> ListAsync(ClaimsPrincipal actor, string? search = null,
        CancellationToken cancellationToken = default);

    /// <summary>Blocks a citizen from submitting anything; reading the public area stays open.</summary>
    Task BlockAsync(string profileId, string reason, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task UnblockAsync(string profileId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
