using System.Security.Claims;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Common;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>
/// Verwaltung der zentralen Dokumenten-Bibliothek (im WYSIWYG-Editor erstellte HTML-Dokumente).
/// Sichtbarkeit nach Verschluss-Stufe: „nur Führung" sieht die Führung, „nur TRU"/„nur HRB" zusätzlich
/// die jeweilige Einheit (die Führung sieht stets alle Stufen).
/// </summary>
public interface IDocumentService
{
    /// <summary>Visible documents, newest first; classified-filtered, or released-only when partnerAgency is set.</summary>
    Task<List<DocumentListItem>> GetListAsync(DocumentViewerScope scope, CancellationToken cancellationToken = default, PartnerAgency? partnerAgency = null);

    /// <summary>Typeahead-Suche über Titel/Kategorie für die Auswahl beim Anhängen.</summary>
    Task<List<DocumentListItem>> SearchAsync(string? searchText, DocumentViewerScope scope, int max = 20, CancellationToken cancellationToken = default);

    /// <summary>Single document with HTML body, or null if missing/not visible; released-only when partnerAgency is set.</summary>
    Task<Document?> GetAsync(string id, DocumentViewerScope scope, CancellationToken cancellationToken = default, PartnerAgency? partnerAgency = null);

    Task<Document> CreateAsync(DocumentInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task RefreshAsync(string id, DocumentInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Setzt/entfernt die „Angepinnt"-Markierung (erscheint oben in der Bibliothek). Nur Führung.</summary>
    Task PinSetAsync(string id, bool pinned, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Soft-Delete (Papierkorb). Nur Ersteller oder Führung.</summary>
    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Akten, an die dieses Dokument als Quelle angehängt ist (für die „Angehängt an"-Anzeige).
    /// <paramref name="meId"/> = Agent-Id des Betrachters (fremde Taskforces werden ausgeblendet).</summary>
    Task<List<DocumentAttachment>> GetAttachmentsAsync(string documentId, bool isLeadership, string? meId, CancellationToken cancellationToken = default);
}
