using System.Security.Claims;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Services.Public;

/// <summary>Public organisation profiles: the publication snapshot of a faction file and what decides whether it is outside.</summary>
public interface IPublicFactionProfileService
{
    // ---- outward reads (anonymous) ----

    /// <summary>Every published profile, cached; empty while the module is off.</summary>
    Task<PublicFactionBoard> GetBoardAsync(CancellationToken cancellationToken = default);

    // ---- internal reads ----

    /// <summary>Profile of a faction file, for the warning banner; null when there is none.</summary>
    Task<PublicFactionProfileBanner?> GetBannerForFactionAsync(string factionId, CancellationToken cancellationToken = default);

    /// <summary>The management list across factions.</summary>
    Task<IReadOnlyList<PublicFactionProfileEdit>> GetAllAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Profile of one faction file, whatever its state; null when there is none.</summary>
    Task<PublicFactionProfileEdit?> GetForFactionAsync(string factionId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>The one profile being edited, description included.</summary>
    Task<PublicFactionProfileDraft?> GetDraftAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    // ---- writes ----

    /// <summary>Create a draft from a faction file and return its id; pulls the name only.</summary>
    Task<string> CreateDraftFromFactionAsync(string factionId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Update the snapshot fields of a profile.</summary>
    Task UpdateSnapshotAsync(PublicFactionProfileInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Put a profile outside; refuses a classified or deleted faction file.</summary>
    Task PublishAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Take a profile offline with a reason; works while the module is off.</summary>
    Task RetractAsync(string id, string reason, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Recompute the published hazard level from the faction's current score.</summary>
    Task RefreshHazardLevelAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Soft-delete a profile; refused while it is published.</summary>
    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Take the profile of a record offline because the record itself changed; no rights guard, the caller passed one.</summary>
    Task RetractForRecordAsync(string factionId, string reason, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    // ---- trash ----

    Task<List<OeffentlichesFraktionsprofil>> GetTrashAsync(CancellationToken cancellationToken = default);

    Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
