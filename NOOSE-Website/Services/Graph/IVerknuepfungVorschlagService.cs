using System.Security.Claims;
using NOOSE_Website.Models.Graph;

namespace NOOSE_Website.Services;

/// <summary>
/// Schlägt für eine Akte verwandte, noch nicht verknüpfte Akten vor (Phase 8). Block A: personenzentriert –
/// erkennt Kandidaten über gemeinsame Telefonnummer, Fraktion, Personengruppe, Tag und gemeinsame
/// Verknüpfungen. Bereits Verknüpfte/Bezogene sowie nicht sichtbare Akten (Verschlusssache) werden
/// ausgeschlossen.
/// </summary>
public interface IVerknuepfungVorschlagService
{
    /// <summary>Liefert die ranggeordneten Vorschläge für die angegebene Akte (leer, wenn keine).</summary>
    Task<List<VerknuepfungVorschlag>> GetVorschlaegeAsync(string entitaetTyp, string entitaetId, ClaimsPrincipal betrachter, CancellationToken cancellationToken = default);
}
