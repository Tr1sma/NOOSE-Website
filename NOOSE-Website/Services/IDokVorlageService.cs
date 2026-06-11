using System.Security.Claims;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Models.Personen;

namespace NOOSE_Website.Services;

/// <summary>
/// Verwaltung der Dok-Vorlagen (admin-definierte Erfassungsmasken, Plan.md Phase 7). Anlegen/Ändern/Löschen
/// ist der Führung vorbehalten; aktive Vorlagen darf jeder aktive Agent beim Dok-Anlegen nutzen.
/// </summary>
public interface IDokVorlageService
{
    /// <summary>Alle Vorlagen (inkl. inaktiver) für die Verwaltung, sortiert nach Sortierung/Name.</summary>
    Task<List<DokVorlage>> GetAlleAsync(CancellationToken cancellationToken = default);

    /// <summary>Nur aktive Vorlagen für den Picker beim Dok-Anlegen, sortiert nach Sortierung/Name.</summary>
    Task<List<DokVorlage>> GetAktiveAsync(CancellationToken cancellationToken = default);

    Task<DokVorlage> ErstellenAsync(DokVorlageEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    Task AktualisierenAsync(string id, DokVorlageEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Löscht eine Vorlage (Soft-Delete → Papierkorb).</summary>
    Task LoeschenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
}
