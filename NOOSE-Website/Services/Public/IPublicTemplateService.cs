using System.Security.Claims;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Services.Public;

/// <summary>Templates for citizen-facing messages: the settings panel, the pickers and the automatic confirmations.</summary>
/// <remarks>
/// The read paths carry no guard on purpose — a template is agency boilerplate rather than record content, and the
/// automatic confirmation is read while a citizen is acting, so a guard would answer the confirmation with an
/// UnauthorizedAccessException. Same reasoning as IDocumentTemplateService.GetActiveAsync and
/// ITicketService.GetOpenCountAsync: the caller is already the gate.
/// </remarks>
public interface IPublicTemplateService
{
    /// <summary>Every living template, grouped order first; for the settings panel.</summary>
    Task<IReadOnlyList<PublicTemplateRow>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Active templates of one kind, in picker order.</summary>
    Task<IReadOnlyList<PublicTemplateRow>> GetActiveAsync(PublicTemplateKind kind,
        CancellationToken cancellationToken = default);

    /// <summary>The one template an automatic message uses, or null when none is active.</summary>
    Task<PublicTemplateRow?> GetAutomaticAsync(PublicTemplateKind kind, CancellationToken cancellationToken = default);

    Task<PublicTemplateRow?> GetAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates; tokens are kept raw, foreign ones are refused.</summary>
    Task<string> SaveAsync(PublicTemplateInput input, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    Task SetActiveAsync(string id, bool active, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
