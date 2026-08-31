using System.Security.Claims;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Services.Public;

/// <summary>The released monthly situation reports: draft, publish, retract.</summary>
/// <remarks>
/// The public text of a month, not its figures. The internal monthly snapshot stays with
/// <c>ISituationReportService</c>; this service never takes that dependency, because the public pages inject it and
/// would otherwise build the whole statistics stack into an anonymous visitor's object graph.
/// </remarks>
public interface IPublicReportService
{
    /// <summary>Everything published, cached; empty while the module is off.</summary>
    Task<PublicReportSnapshot> GetPublishedAsync(CancellationToken cancellationToken = default);

    /// <summary>One published report by its period (2026-08); null for anything else.</summary>
    Task<PublicReportView?> GetByPeriodAsync(string? period, CancellationToken cancellationToken = default);

    /// <summary>All reports including drafts, for the settings panel; carries no HTML.</summary>
    Task<IReadOnlyList<PublicReportEdit>> GetAllAsync(ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>Archived monthly reports that have no living public text yet.</summary>
    Task<IReadOnlyList<PublicReportAnchor>> GetAnchorsAsync(ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>The draft of one report, for the editor; null when it is gone.</summary>
    Task<PublicReportDraft?> GetDraftAsync(string id, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a draft and returns the row id; never touches the published copy.</summary>
    Task<string> SaveDraftAsync(PublicReportInput input, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>Copies the draft onto the published copy.</summary>
    Task PublishAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Takes the report off the public area; draft and published copy stay.</summary>
    Task RetractAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task<List<OeffentlicherLagebericht>> GetTrashAsync(CancellationToken cancellationToken = default);

    Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
