using System.Security.Claims;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Services.Public;

/// <summary>Press releases of the agency: draft, publish, retract.</summary>
public interface IPressReleaseService
{
    /// <summary>Everything currently published, cached; empty while the module is off.</summary>
    Task<PublicPressSnapshot> GetPublishedAsync(CancellationToken cancellationToken = default);

    /// <summary>The published release, or null when it does not exist, is retracted, or its module is off.</summary>
    Task<PublicPressView?> GetByCaseNumberAsync(string? caseNumber, CancellationToken cancellationToken = default);

    /// <summary>All releases including drafts, for the settings panel; carries no HTML.</summary>
    Task<IReadOnlyList<PressEdit>> GetAllAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>The draft of one release, for the editor; null when it is gone.</summary>
    Task<PressDraft?> GetDraftAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a draft and returns the row id; never touches the published copy.</summary>
    Task<string> SaveDraftAsync(PressInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Copies the draft onto the published copy, mints the case number on the first publication.</summary>
    Task PublishAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Takes the release off the public area; the draft, the published copy and the case number stay.</summary>
    Task RetractAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Leaves a draft behind after a notice was closed; never publishes.</summary>
    /// <remarks>
    /// Takes the outward card of the notice, so the draft cannot name a record, an agent or a score. Guarded by the
    /// authority that closes a notice, not by the press guard: a rank-2 agent may set a notice to captured, and the
    /// draft is agency output rather than something that actor publishes. Does nothing while the press module is
    /// switched off: a draft exists to be published, and one per capture that nobody can publish is noise rather than
    /// a safety net.
    /// </remarks>
    Task CreateCaptureDraftAsync(PublicWantedCard notice, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task<List<Pressemitteilung>> GetTrashAsync(CancellationToken cancellationToken = default);

    Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
