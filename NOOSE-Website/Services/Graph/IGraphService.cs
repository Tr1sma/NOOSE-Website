using System.Security.Claims;
using NOOSE_Website.Models.Graph;

namespace NOOSE_Website.Services;

/// <summary>
/// Baut den Beziehungsgraph (Phase 8) aus den vorhandenen Kanten-Quellen <c>Verknuepfung</c> und
/// <c>PersonBeziehung</c> auf – rein lesend, ohne eigenes Schema. Sämtliche Knoten werden gegen die
/// zentrale Sichtbarkeit (Verschlusssache/Taskforce/Papierkorb) geprüft; nicht sichtbare Akten und die
/// daran hängenden Kanten erscheinen weder im Graph noch in der Pfadsuche.
/// </summary>
public interface IGraphService
{
    /// <summary>
    /// Liefert den Graph für die Anfrage. Ohne Fokus den (auf die wichtigsten Knoten gedeckelten)
    /// Gesamtgraph, mit Fokus den Umkreis des Fokusknotens bis zur gewünschten Tiefe.
    /// </summary>
    Task<GraphData> GetGraphAsync(GraphQuery query, ClaimsPrincipal viewer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sucht den kürzesten (sichtbaren) Pfad zwischen zwei Akten („Wie hängen A und B zusammen?").
    /// Liefert <see cref="PfadErgebnis.Gefunden"/> = false, wenn es keine Verbindung gibt.
    /// </summary>
    Task<PathResult> FindPathAsync(string sourceType, string sourceId, string targetType, string targetId, ClaimsPrincipal viewer, CancellationToken cancellationToken = default);
}
