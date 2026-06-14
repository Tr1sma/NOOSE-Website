using System.Security.Claims;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <summary>Verwaltung der admin-definierten Custom-Feld-Definitionen (Zusatzfelder je Aktentyp).</summary>
public interface ICustomFieldDefinitionService
{
    /// <summary>Alle Definitionen (Admin-Liste, inkl. inaktive), sortiert nach Aktentyp/Reihenfolge/Name.</summary>
    Task<List<CustomFieldDefinition>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Definitionen eines Aktentyps; <paramref name="nurAktive"/> für das Panel.</summary>
    Task<List<CustomFieldDefinition>> GetForTypeAsync(string entityType, bool onlyActive, CancellationToken cancellationToken = default);

    Task<CustomFieldDefinition> CreateAsync(CustomFieldDefinitionInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task RefreshAsync(string id, CustomFieldDefinitionInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
