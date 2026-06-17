using System.Security.Claims;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Models.People;

namespace NOOSE_Website.Services;

/// <summary>Management of doc templates; create/edit/delete is leadership-only, active templates are usable by any active agent.</summary>
public interface IDocTemplateService
{
    /// <summary>All templates (including inactive) for management, sorted by order/name.</summary>
    Task<List<DocTemplate>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Active templates only for the create-doc picker, sorted by order/name.</summary>
    Task<List<DocTemplate>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<DocTemplate> CreateAsync(DocTemplateInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task RefreshAsync(string id, DocTemplateInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a template (to trash).</summary>
    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
