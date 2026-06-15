using System.Security.Claims;

namespace NOOSE_Website.Services;

/// <summary>
/// Lädt &amp; speichert die admin-einstellbare <see cref="BedrohungsScoreKonfiguration"/> (Phase 8/Block D).
/// Muster wie <c>AktualitaetService</c>: Code-Default als Fallback, DB-Overlay, 10-Minuten-Cache,
/// <c>Berechtigung.VerlangeFuehrung</c> + volle Validierung beim Speichern.
/// </summary>
public interface IThreatScoreConfigService
{
    /// <summary>Aktuelle Konfiguration für die Berechnung (gecacht). Aufrufer dürfen die Instanz NICHT mutieren.</summary>
    Task<ThreatScoreConfiguration> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Frische, mutierbare Kopie für die Admin-Bearbeitung (nicht gecacht).</summary>
    Task<ThreatScoreConfiguration> GetEditableAsync(CancellationToken cancellationToken = default);

    /// <summary>Validiert und speichert die Konfiguration (Führung erforderlich) und invalidiert den Cache.</summary>
    Task SaveAsync(ThreatScoreConfiguration config, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
