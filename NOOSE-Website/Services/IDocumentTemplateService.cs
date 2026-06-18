using System.Security.Claims;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <summary>Management of document templates (HTML body with placeholders); writes are leadership-only.</summary>
public interface IDocumentTemplateService
{
    /// <summary>All templates for management, sorted.</summary>
    Task<List<DocumentTemplate>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Active templates only for the create-document picker.</summary>
    Task<List<DocumentTemplate>> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>A single template with HTML body, or null if missing.</summary>
    Task<DocumentTemplate?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task<DocumentTemplate> CreateAsync(DocumentTemplateInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task RefreshAsync(string id, DocumentTemplateInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
