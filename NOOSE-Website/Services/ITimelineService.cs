using System.Security.Claims;
using NOOSE_Website.Models.Timeline;

namespace NOOSE_Website.Services;

/// <summary>
/// Liefert den vereinheitlichten, chronologischen Zeitstrahl einer Akte: die strukturellen Audit-Ereignisse
/// (Anlage/Änderung/Mitgliedschaft/Zuteilung) zusammengeführt mit den semantischen Domänen-Ereignissen
/// (Einstufung, Doks, Observationen, Verknüpfungen, Kommentare, Quellen, Wiedervorlagen, …). Rein lesend.
/// Ersetzt fachlich den bisherigen je-Akte „Historie"-Reiter (reines Audit-Log).
/// </summary>
public interface ITimelineService
{
    /// <summary>Alle sichtbaren Ereignisse der Akte, absteigend nach Zeitpunkt. Leere Liste, wenn der
    /// Betrachter die Akte nicht sehen darf (Verschlusssache/Papierkorb/Taskforce/eingeschränkte Aufgabe).</summary>
    Task<IReadOnlyList<TimelineEntry>> GetTimelineAsync(
        string entityType, string entityId, ClaimsPrincipal viewer,
        CancellationToken cancellationToken = default);
}
