using System.Security.Claims;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <summary>
/// Verwaltung der Dokument-Vorlagen (HTML-Body mit Platzhaltern). Schreibende Aktionen sind der Führung
/// vorbehalten (analog zu <see cref="IDokVorlageService"/>).
/// </summary>
public interface IDocumentTemplateService
{
    /// <summary>Alle Vorlagen (für die Verwaltung), sortiert.</summary>
    Task<List<DocumentTemplate>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Nur aktive Vorlagen (für den Picker beim Dokument-Anlegen).</summary>
    Task<List<DocumentTemplate>> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Eine einzelne Vorlage inkl. HTML-Body, oder null wenn nicht vorhanden.</summary>
    Task<DocumentTemplate?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task<DocumentTemplate> CreateAsync(DocumentTemplateInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task RefreshAsync(string id, DocumentTemplateInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
