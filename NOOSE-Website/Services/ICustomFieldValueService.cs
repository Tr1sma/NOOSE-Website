using System.Security.Claims;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <summary>Liest und speichert die Custom-Feld-Werte einer einzelnen Akte (polymorph).</summary>
public interface ICustomFieldValueService
{
    /// <summary>Active definitions of the record type with the record's current values, in display order; partner-gated when scope is a partner.</summary>
    Task<List<CustomFieldValueDisplay>> GetForRecordAsync(string entityType, string entityId, CancellationToken cancellationToken = default, ViewerScope? scope = null);

    /// <summary>Speichert die Werte (Upsert; leere Werte werden entfernt). Prüft Pflichtfelder.</summary>
    /// <param name="werteJeDefinition">Definition-Id → Wert (String; null/leer = nicht gesetzt).</param>
    Task SetAsync(string entityType, string entityId, IReadOnlyDictionary<string, string?> valuesPerDefinition,
        ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
