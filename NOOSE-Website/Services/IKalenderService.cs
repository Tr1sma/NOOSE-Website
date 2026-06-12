using System.Security.Claims;
using NOOSE_Website.Models.Kalender;

namespace NOOSE_Website.Services;

/// <summary>
/// Stellt die Kalender-Einträge eines Zeitfensters zusammen – rein lesend. Aggregiert die eigene Termin-Akte
/// mit bestehenden datierten Akten (Operationen, Überwachungsfenster, fällige Aufgaben/Wiedervorlagen,
/// Fraktions-Aktivitäten). Jede Quelle behält ihre kanonische Sichtbarkeit (Verschlusssache bzw. Aufgaben-/
/// Termin-„Eingeschränkt"); nicht sichtbare Einträge fehlen im Ergebnis (kein Leak).
/// </summary>
public interface IKalenderService
{
    /// <summary>Alle für den Betrachter sichtbaren Kalender-Einträge der gewählten Sicht (<paramref name="modus"/>),
    /// die das UTC-Fenster [<paramref name="vonUtc"/>, <paramref name="bisUtc"/>] berühren.</summary>
    Task<IReadOnlyList<KalenderEintrag>> GetEintraegeAsync(
        DateTime vonUtc, DateTime bisUtc, ClaimsPrincipal betrachter, KalenderModus modus, CancellationToken cancellationToken = default);
}
