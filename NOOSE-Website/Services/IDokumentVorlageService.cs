using System.Security.Claims;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Models.Querschnitt;

namespace NOOSE_Website.Services;

/// <summary>
/// Verwaltung der Dokument-Vorlagen (HTML-Body mit Platzhaltern). Schreibende Aktionen sind der Führung
/// vorbehalten (analog zu <see cref="IDokVorlageService"/>).
/// </summary>
public interface IDokumentVorlageService
{
    /// <summary>Alle Vorlagen (für die Verwaltung), sortiert.</summary>
    Task<List<DokumentVorlage>> GetAlleAsync(CancellationToken cancellationToken = default);

    /// <summary>Nur aktive Vorlagen (für den Picker beim Dokument-Anlegen).</summary>
    Task<List<DokumentVorlage>> GetAktiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Eine einzelne Vorlage inkl. HTML-Body, oder null wenn nicht vorhanden.</summary>
    Task<DokumentVorlage?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task<DokumentVorlage> ErstellenAsync(DokumentVorlageEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    Task AktualisierenAsync(string id, DokumentVorlageEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    Task LoeschenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
}
