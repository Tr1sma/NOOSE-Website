using System.Security.Claims;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <summary>
/// Verwaltung der zentralen Dokumenten-Bibliothek (im WYSIWYG-Editor erstellte HTML-Dokumente).
/// Sichtbarkeit: Verschlusssachen sind nur für die Führung les-/auswählbar.
/// </summary>
public interface IDocumentService
{
    /// <summary>Alle für den Aufrufer sichtbaren Dokumente (Verschlusssache-gefiltert), neueste zuerst.</summary>
    Task<List<DocumentListItem>> GetListAsync(bool isLeadership, CancellationToken cancellationToken = default);

    /// <summary>Typeahead-Suche über Titel/Kategorie für die Auswahl beim Anhängen.</summary>
    Task<List<DocumentListItem>> SearchAsync(string? searchText, bool isLeadership, int max = 20, CancellationToken cancellationToken = default);

    /// <summary>Ein einzelnes Dokument inkl. HTML-Body, oder null wenn nicht vorhanden/nicht sichtbar.</summary>
    Task<Document?> GetAsync(string id, bool isLeadership, CancellationToken cancellationToken = default);

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
