using System.Security.Claims;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Services.Public;

/// <summary>Editorial pages of the public area: draft, preview, publish, retract.</summary>
public interface IPublicPageService
{
    /// <summary>Everything currently published, cached; empty while the module is off.</summary>
    Task<PublicPageSnapshot> GetPublishedAsync(CancellationToken cancellationToken = default);

    /// <summary>Hub and menu entries in display order.</summary>
    Task<IReadOnlyList<PublicPageLink>> GetMenuAsync(CancellationToken cancellationToken = default);

    /// <summary>The published page, or null when it does not exist, is retracted, or its module is off.</summary>
    Task<PublicPageView?> GetAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>The draft as an agent sees it before publishing; ignores the module switch on purpose.</summary>
    Task<PublicPageView?> GetPreviewAsync(string slug, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>All pages including drafts, for the settings panel; carries no HTML.</summary>
    Task<IReadOnlyList<PublicPageEdit>> GetAllAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>The draft body of one page, for the editor; null when the page is gone.</summary>
    Task<string?> GetDraftAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a draft and returns the row id; never touches the published copy.</summary>
    Task<string> SaveDraftAsync(PublicPageInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Copies the draft onto the published copy and marks the page published.</summary>
    Task PublishAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Takes the page off the public area; the draft and the last published copy stay.</summary>
    Task RetractAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task<List<OeffentlicheSeite>> GetTrashAsync(CancellationToken cancellationToken = default);

    Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
