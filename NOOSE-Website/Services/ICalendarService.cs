using System.Security.Claims;
using NOOSE_Website.Models.Calendar;

namespace NOOSE_Website.Services;

/// <summary>
/// Stellt die Kalender-Einträge eines Zeitfensters zusammen – rein lesend. Aggregiert die eigene Termin-Akte
/// mit bestehenden datierten Akten (Operationen, Überwachungsfenster, fällige Aufgaben/Wiedervorlagen,
/// Fraktions-Aktivitäten). Jede Quelle behält ihre kanonische Sichtbarkeit (Verschlusssache bzw. Aufgaben-/
/// Termin-„Eingeschränkt"); nicht sichtbare Einträge fehlen im Ergebnis (kein Leak).
/// </summary>
public interface ICalendarService
{
    /// <summary>Alle für den Betrachter sichtbaren Kalender-Einträge der gewählten Sicht (<paramref name="modus"/>),
    /// die das UTC-Fenster [<paramref name="vonUtc"/>, <paramref name="bisUtc"/>] berühren.</summary>
    Task<IReadOnlyList<CalendarEntry>> GetEntriesAsync(
        DateTime sourceUtc, DateTime untilUtc, ClaimsPrincipal viewer, CalendarMode mode, CancellationToken cancellationToken = default);
}
