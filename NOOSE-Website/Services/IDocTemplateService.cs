using System.Security.Claims;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Models.People;

namespace NOOSE_Website.Services;

/// <summary>
/// Verwaltung der Dok-Vorlagen (admin-definierte Erfassungsmasken, Plan.md Phase 7). Anlegen/Ändern/Löschen
/// ist der Führung vorbehalten; aktive Vorlagen darf jeder aktive Agent beim Dok-Anlegen nutzen.
/// </summary>
public interface IDocTemplateService
{
    /// <summary>Alle Vorlagen (inkl. inaktiver) für die Verwaltung, sortiert nach Sortierung/Name.</summary>
    Task<List<DocTemplate>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Nur aktive Vorlagen für den Picker beim Dok-Anlegen, sortiert nach Sortierung/Name.</summary>
    Task<List<DocTemplate>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<DocTemplate> CreateAsync(DocTemplateInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task RefreshAsync(string id, DocTemplateInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Löscht eine Vorlage (Soft-Delete → Papierkorb).</summary>
    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
