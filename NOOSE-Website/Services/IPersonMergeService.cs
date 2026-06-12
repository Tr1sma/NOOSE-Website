using System.Security.Claims;

namespace NOOSE_Website.Services;

/// <summary>
/// Duplikat-Zusammenführen zweier Personenakten (Phase 7): überträgt sämtliche Inhalte der
/// Quell-Akte (Doks, Observationen, Fotos, Steckbrief-Kinder, Beziehungen, Verknüpfungen, Tags,
/// Kommentare, Quellen, Custom-Felder, Wiedervorlagen, Watchlist, Mitgliedschaften, Verläufe)
/// auf die Ziel-Akte, übernimmt fehlende Steckbrief-Daten und verschiebt die Quell-Akte in den
/// Papierkorb. Der Name der Quell-Akte bleibt als Alias der Ziel-Akte erhalten. Nur Führung.
/// </summary>
public interface IPersonMergeService
{
    /// <summary>Führt <paramref name="quelleId"/> in <paramref name="zielId"/> zusammen.</summary>
    Task ZusammenfuehrenAsync(string quelleId, string zielId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
}
