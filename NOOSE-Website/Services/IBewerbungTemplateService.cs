using System.Security.Claims;
using NOOSE_Website.Data.Entities.Common;

namespace NOOSE_Website.Services;

/// <summary>CRUD for recruiting message templates (DocumentTemplate, category "Bewerbung"); HRB/leadership may write.</summary>
public interface IBewerbungTemplateService
{
    Task<List<DocumentTemplate>> ListAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task<DocumentTemplate?> GetAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task<DocumentTemplate> CreateAsync(string name, string? description, string contentHtml, bool isActive, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task UpdateAsync(string id, string name, string? description, string contentHtml, bool isActive, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
