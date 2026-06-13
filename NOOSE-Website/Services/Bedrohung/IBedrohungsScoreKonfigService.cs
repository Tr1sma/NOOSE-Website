using System.Security.Claims;

namespace NOOSE_Website.Services;

/// <summary>
/// Lädt &amp; speichert die admin-einstellbare <see cref="BedrohungsScoreKonfiguration"/> (Phase 8/Block D).
/// Muster wie <c>AktualitaetService</c>: Code-Default als Fallback, DB-Overlay, 10-Minuten-Cache,
/// <c>Berechtigung.VerlangeFuehrung</c> + volle Validierung beim Speichern.
/// </summary>
public interface IBedrohungsScoreKonfigService
{
    /// <summary>Aktuelle Konfiguration für die Berechnung (gecacht). Aufrufer dürfen die Instanz NICHT mutieren.</summary>
    Task<BedrohungsScoreKonfiguration> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Frische, mutierbare Kopie für die Admin-Bearbeitung (nicht gecacht).</summary>
    Task<BedrohungsScoreKonfiguration> GetBearbeitbarAsync(CancellationToken cancellationToken = default);

    /// <summary>Validiert und speichert die Konfiguration (Führung erforderlich) und invalidiert den Cache.</summary>
    Task SpeichernAsync(BedrohungsScoreKonfiguration konfig, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
}
