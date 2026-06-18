using System.Security.Claims;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <summary>Reads and stores the custom field values of a single record (polymorphic).</summary>
public interface ICustomFieldValueService
{
    /// <summary>Active definitions of the record type with the record's current values, in display order; partner-gated when scope is a partner.</summary>
    Task<List<CustomFieldValueDisplay>> GetForRecordAsync(string entityType, string entityId, CancellationToken cancellationToken = default, ViewerScope? scope = null);

    /// <summary>Upserts the values (empty values are removed); validates required fields.</summary>
    Task SetAsync(string entityType, string entityId, IReadOnlyDictionary<string, string?> valuesPerDefinition,
        ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
