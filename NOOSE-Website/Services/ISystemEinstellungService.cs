using System.Security.Claims;
using NOOSE_Website.Models.Querschnitt;

namespace NOOSE_Website.Services;

/// <summary>
/// Systemeinstellungen (Phase 7): Wartungsmodus, Ankündigungsbanner, Theme-Farben und Logo-Upload.
/// Lesen ist gecacht (jeder Request/Circuit fragt die Konfiguration ab); Schreiben ist Admins
/// vorbehalten (technische Systemverwaltung, Plan.md §6) und invalidiert den Cache.
/// </summary>
public interface ISystemEinstellungService
{
    /// <summary>Aktuelle Konfiguration (gecacht, ~10 s). Fällt bei DB-Fehlern auf die Standardwerte zurück.</summary>
    Task<SystemKonfiguration> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Speichert Wartungsmodus/Banner/Theme-Farben. Nur Admin; Farben müssen leer oder #RRGGBB sein.</summary>
    Task SpeichernAsync(SystemKonfigurationEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Lädt ein neues Logo hoch (nur Bilder, Größenlimit wie Foto-Upload). Nur Admin.</summary>
    Task LogoSetzenAsync(Stream inhalt, string originalName, string contentType, long groesseBytes, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Entfernt das hochgeladene Logo (zurück zum Standard-Wappen). Nur Admin.</summary>
    Task LogoEntfernenAsync(ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Öffnet die aktuelle Logo-Datei für die Auslieferung; <c>null</c>, wenn keines gesetzt ist.</summary>
    Task<(Stream Inhalt, string ContentType)?> LogoOeffnenAsync(CancellationToken cancellationToken = default);
}
