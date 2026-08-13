using System.Security.Claims;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Services.Public;

/// <summary>Public wanted notices: the publication snapshot of a person file and everything that decides whether it is outside.</summary>
public interface IPublicWantedService
{
    // ---- outward reads (anonymous) ----

    /// <summary>The whole published board, cached; empty while the module is off.</summary>
    Task<PublicWantedBoard> GetBoardAsync(CancellationToken cancellationToken = default);

    /// <summary>One published notice by its case number; null for every other state.</summary>
    Task<PublicWantedDetail?> GetByCaseNumberAsync(string? caseNumber, CancellationToken cancellationToken = default);

    /// <summary>The photo copy of a published notice; null for every miss, so the endpoint cannot become an existence oracle.</summary>
    Task<PublicWantedPhoto?> GetPublishedPhotoAsync(string? caseNumber, CancellationToken cancellationToken = default);

    // ---- internal reads ----

    /// <summary>Newest live notice of a person file, for the warning banner; null when there is none.</summary>
    Task<PublicWantedBanner?> GetBannerForPersonAsync(string personId, CancellationToken cancellationToken = default);

    /// <summary>The management list.</summary>
    Task<IReadOnlyList<PublicWantedEdit>> GetAllAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>The one notice being edited, accusation HTML included.</summary>
    Task<PublicWantedDraft?> GetDraftAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Photo and area choices the editor may offer for a notice; the component never reads the file itself.</summary>
    Task<PublicWantedOptions> GetOptionsAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Newest notice of a person file for the file page, whatever its state; null when there is none.</summary>
    Task<PublicWantedEdit?> GetForPersonAsync(string personId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    // ---- writes ----

    /// <summary>Create a draft from a person file and return its id; pulls name and accusation only.</summary>
    Task<string> CreateDraftFromPersonAsync(string personId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Update the snapshot fields of a notice.</summary>
    Task UpdateSnapshotAsync(PublicWantedInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Publish directly (rank ≥ 3) or file a request (rank 1-2); refuses a classified or deleted file either way.</summary>
    Task<PublicWantedPublishOutcome> PublishAsync(string id, string? justification, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>Take a notice offline with a reason; works while the module is off.</summary>
    Task RetractAsync(string id, string reason, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Mark a published notice as captured.</summary>
    Task CapturedAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Recompute the published hazard level from the file's current score.</summary>
    Task RefreshHazardLevelAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Soft-delete a notice; refused while it is published.</summary>
    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Take every live notice of a record offline because the record itself changed; no rights guard, the caller passed one.</summary>
    Task RetractForRecordAsync(string personId, string reason, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    // ---- trash ----

    Task<List<OeffentlicheFahndung>> GetTrashAsync(CancellationToken cancellationToken = default);

    Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    // ---- publication requests ----

    Task<IReadOnlyList<PublicWantedRequestRow>> GetPendingRequestsAsync(CancellationToken cancellationToken = default);

    Task<int> GetPendingRequestCountAsync(CancellationToken cancellationToken = default);

    /// <summary>Approve a pending request and publish through the same body a direct publication uses.</summary>
    Task ApprovePublicationRequestAsync(string requestId, string? note, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>Reject a pending request; the notice falls back to a draft.</summary>
    Task RejectPublicationRequestAsync(string requestId, string? note, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);
}
