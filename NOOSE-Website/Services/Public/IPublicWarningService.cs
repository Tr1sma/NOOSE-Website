using System.Security.Claims;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Services.Public;

/// <summary>Standing public warnings of the agency: draft, publish, retract.</summary>
/// <remarks>
/// Not <see cref="IWarnhinweisService"/>, which keeps the chip labels of a wanted notice. Two tables, two meanings.
/// </remarks>
public interface IPublicWarningService
{
    /// <summary>Everything published and still valid, cached; empty while the module is off.</summary>
    Task<PublicWarningSnapshot> GetPublishedAsync(CancellationToken cancellationToken = default);

    /// <summary>All warnings including drafts and expired ones, for the settings panel; carries no HTML.</summary>
    Task<IReadOnlyList<WarningEdit>> GetAllAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>The draft of one warning, for the editor; null when it is gone.</summary>
    Task<WarningDraft?> GetDraftAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a draft and returns the row id; never touches the published copy.</summary>
    Task<string> SaveDraftAsync(WarningInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Copies the draft onto the published copy.</summary>
    Task PublishAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Takes the warning off the public area; draft and published copy stay.</summary>
    Task RetractAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task<List<OeffentlicheWarnung>> GetTrashAsync(CancellationToken cancellationToken = default);

    Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
