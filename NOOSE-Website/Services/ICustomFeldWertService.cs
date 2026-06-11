using System.Security.Claims;
using NOOSE_Website.Models.Querschnitt;

namespace NOOSE_Website.Services;

/// <summary>Liest und speichert die Custom-Feld-Werte einer einzelnen Akte (polymorph).</summary>
public interface ICustomFeldWertService
{
    /// <summary>Aktive Definitionen des Aktentyps samt aktuellem Wert der Akte, in Anzeigereihenfolge.</summary>
    Task<List<CustomFeldWertAnzeige>> GetFuerAkteAsync(string entitaetTyp, string entitaetId, CancellationToken cancellationToken = default);

    /// <summary>Speichert die Werte (Upsert; leere Werte werden entfernt). Prüft Pflichtfelder.</summary>
    /// <param name="werteJeDefinition">Definition-Id → Wert (String; null/leer = nicht gesetzt).</param>
    Task SetzenAsync(string entitaetTyp, string entitaetId, IReadOnlyDictionary<string, string?> werteJeDefinition,
        ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
}
