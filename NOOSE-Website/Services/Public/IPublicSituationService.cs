using System.Security.Claims;
using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Services.Public;

/// <summary>The situation level the agency publishes on /lage.</summary>
/// <remarks>
/// Stored as four rows in the settings table rather than a table of its own, the way the kill switch is: there is
/// exactly one of these, ever. The module switch decides whether it is outside; there is no draft step.
/// </remarks>
public interface IPublicSituationService
{
    /// <summary>What an anonymous visitor may read; null when nothing is being said.</summary>
    /// <remarks>
    /// Null covers three cases on purpose — the module is off, no level has ever been set, and the database is
    /// unreachable. None of them may answer with a level: a default "Niedrig" would be the agency claiming there is
    /// no danger, which is a statement, not silence.
    /// </remarks>
    Task<PublicSituationState?> GetPublishedAsync(CancellationToken cancellationToken = default);

    /// <summary>The stored state for the settings panel, whatever the module switch says.</summary>
    Task<PublicSituationState?> GetForEditAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Sets level and assessment; the date and the previous level are derived, never supplied.</summary>
    Task SetAsync(PublicSituationInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
