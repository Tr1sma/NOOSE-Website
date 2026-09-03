using System.Security.Claims;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Models.Public;
using NOOSE_Website.Models.Recruiting;

namespace NOOSE_Website.Services.Public;

/// <summary>Citizen accounts of the public area: own profile, and the agency-side roster with blocking.</summary>
public interface IBuergerService
{
    /// <summary>The signed-in citizen's own profile, or null when none exists yet.</summary>
    Task<BuergerProfil?> GetOwnAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Sets the signed-in citizen's own name — once. A later change is refused; see <see cref="SetNameAsync"/>.</summary>
    /// <remarks>
    /// The name is an identity claim elsewhere (the objection gate compares it against a published notice), so it is
    /// settled at the first save rather than freely rewritable. <c>BuergerNameRules</c> is the one place that decides.
    /// </remarks>
    Task<BuergerProfil> SaveOwnAsync(string firstName, string lastName, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>Corrects a citizen's settled name; leadership only, and audited like every other write here.</summary>
    Task SetNameAsync(string profileId, string firstName, string lastName, ClaimsPrincipal actor,
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

    /// <summary>Shuts the account out of the site entirely: the login refuses and a running session is evicted.</summary>
    /// <remarks>
    /// A step above <see cref="BlockAsync"/>, which only stops submissions. This is the answer to a troll name — the
    /// consequence the naming form warns about. Citizen accounts only; an agent is blocked from the agent roster.
    /// </remarks>
    Task BlockAccountAsync(string profileId, string reason, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>Lets a shut-out account back in as a citizen; never as an agent.</summary>
    Task UnblockAccountAsync(string profileId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Ties a citizen account to a person file, or unties it when personId is null.</summary>
    /// <remarks>
    /// Leadership work: it joins a civilian identity to the record stock. Unlike the application twin it checks that
    /// the target is visible to the actor, so a classified file cannot be linked by someone who may not open it.
    /// </remarks>
    Task LinkPersonAsync(string profileId, string? personId, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>The linked person file, or null when there is none or the caller may not see it.</summary>
    Task<LinkedPersonInfo?> GetLinkedPersonAsync(string profileId, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>Recounts the citizen's confirmed tips, the trust tier behind the quota and the inbox order.</summary>
    /// <remarks>
    /// Recomputed rather than incremented: the status whitelist allows a tip back into review and confirmed again, so an
    /// increment would count one tip twice and stay too high after a retraction. No actor — the number is derived, and
    /// every caller is a guarded write path.
    /// </remarks>
    Task RecomputeConfirmedTipsAsync(string profileId, CancellationToken cancellationToken = default);
}
