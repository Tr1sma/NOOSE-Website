using System.Security.Claims;
using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Services.Public;

/// <summary>The structured FAQ of the public area: sections, their questions, and what the outside world reads.</summary>
/// <remarks>
/// No draft/published pair here, unlike a press release or a warning: a FAQ answer is not a dated statement, so a
/// second click to publish would only be a way to forget one. Visibility is a switch per section and per question,
/// and saving takes effect at once.
/// </remarks>
public interface IPublicFaqService
{
    /// <summary>Everything visible, cached; empty while the module is off or the FAQ page is not published.</summary>
    Task<PublicFaqSnapshot> GetPublishedAsync(CancellationToken cancellationToken = default);

    /// <summary>The same, plus what is switched off, for an agent's preview; ignores both gates on purpose.</summary>
    Task<PublicFaqSnapshot> GetPreviewAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Everything the editorial panel needs; the questions carry no answer.</summary>
    Task<PublicFaqAdminView> GetAllAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>The answer of one question, for the editor; null when the question is gone.</summary>
    Task<string?> GetAnswerAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a section and returns its id.</summary>
    Task<string> SaveRubrikAsync(PublicFaqRubrikInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a question and returns its id; the anchor is minted once, at creation.</summary>
    Task<string> SaveEntryAsync(PublicFaqEntryInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task SetRubrikVisibleAsync(string id, bool visible, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task SetEntryVisibleAsync(string id, bool visible, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Moves a section one step; <paramref name="delta"/> is -1 or 1.</summary>
    Task MoveRubrikAsync(string id, int delta, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Moves a question one step inside its own section.</summary>
    Task MoveEntryAsync(string id, int delta, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Deletes a section; refused while it still holds questions.</summary>
    Task DeleteRubrikAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task DeleteEntryAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
