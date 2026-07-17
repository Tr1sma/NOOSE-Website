using System.Security.Claims;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Common;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Management of personnel-record templates (HTML body); writes are leadership-only.</summary>
public interface IPersonnelTemplateService
{
    /// <summary>All templates for management, sorted by kind then order.</summary>
    Task<List<PersonnelTemplate>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Active templates of one kind for the create picker.</summary>
    Task<List<PersonnelTemplate>> GetActiveAsync(PersonnelTemplateKind kind, CancellationToken cancellationToken = default);

    /// <summary>A single template with HTML body, or null if missing.</summary>
    Task<PersonnelTemplate?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task<PersonnelTemplate> CreateAsync(PersonnelTemplateInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task RefreshAsync(string id, PersonnelTemplateInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
