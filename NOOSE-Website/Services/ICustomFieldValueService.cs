using System.Security.Claims;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <summary>Liest und speichert die Custom-Feld-Werte einer einzelnen Akte (polymorph).</summary>
public interface ICustomFieldValueService
{
    /// <summary>Aktive Definitionen des Aktentyps samt aktuellem Wert der Akte, in Anzeigereihenfolge.</summary>
    Task<List<CustomFieldValueDisplay>> GetForRecordAsync(string entityType, string entityId, CancellationToken cancellationToken = default);

    /// <summary>Speichert die Werte (Upsert; leere Werte werden entfernt). Prüft Pflichtfelder.</summary>
    /// <param name="werteJeDefinition">Definition-Id → Wert (String; null/leer = nicht gesetzt).</param>
    Task SetAsync(string entityType, string entityId, IReadOnlyDictionary<string, string?> valuesPerDefinition,
        ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
