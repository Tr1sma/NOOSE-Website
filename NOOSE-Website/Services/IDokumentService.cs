using System.Security.Claims;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Models.Querschnitt;

namespace NOOSE_Website.Services;

/// <summary>
/// Verwaltung der zentralen Dokumenten-Bibliothek (im WYSIWYG-Editor erstellte HTML-Dokumente).
/// Sichtbarkeit: Verschlusssachen sind nur für die Führung les-/auswählbar.
/// </summary>
public interface IDokumentService
{
    /// <summary>Alle für den Aufrufer sichtbaren Dokumente (Verschlusssache-gefiltert), neueste zuerst.</summary>
    Task<List<DokumentListeItem>> GetListeAsync(bool istFuehrung, CancellationToken cancellationToken = default);

    /// <summary>Typeahead-Suche über Titel/Kategorie für die Auswahl beim Anhängen.</summary>
    Task<List<DokumentListeItem>> SucheAsync(string? suchtext, bool istFuehrung, int max = 20, CancellationToken cancellationToken = default);

    /// <summary>Ein einzelnes Dokument inkl. HTML-Body, oder null wenn nicht vorhanden/nicht sichtbar.</summary>
    Task<Dokument?> GetAsync(string id, bool istFuehrung, CancellationToken cancellationToken = default);

    Task<Dokument> ErstellenAsync(DokumentEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    Task AktualisierenAsync(string id, DokumentEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Soft-Delete (Papierkorb). Nur Ersteller oder Führung.</summary>
    Task LoeschenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Akten, an die dieses Dokument als Quelle angehängt ist (für die „Angehängt an"-Anzeige).</summary>
    Task<List<DokumentAnhang>> GetAnhaengeAsync(string dokumentId, bool istFuehrung, CancellationToken cancellationToken = default);
}
