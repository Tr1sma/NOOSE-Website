using System.Security.Claims;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <summary>Management of admin-defined custom field definitions per record type.</summary>
public interface ICustomFieldDefinitionService
{
    /// <summary>All definitions (admin list, including inactive), sorted by record type/order/name.</summary>
    Task<List<CustomFieldDefinition>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Definitions for a record type; onlyActive for the panel.</summary>
    Task<List<CustomFieldDefinition>> GetForTypeAsync(string entityType, bool onlyActive, CancellationToken cancellationToken = default);

    Task<CustomFieldDefinition> CreateAsync(CustomFieldDefinitionInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task RefreshAsync(string id, CustomFieldDefinitionInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
