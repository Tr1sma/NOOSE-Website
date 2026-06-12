using System.Security.Claims;
using NOOSE_Website.Data.Entities.Querschnitt;

namespace NOOSE_Website.Services;

/// <summary>
/// Zentrale Datei-Bibliothek (Phase 7): durchsuchbare Ablage hochgeladener Dateien (Formulare,
/// SOPs, Vorlagen). Hochladen dürfen alle schreibberechtigten Agenten; Löschen und das
/// Verschlusssache-Flag sind der Führung vorbehalten. Verschlusssachen sieht nur die Führung.
/// </summary>
public interface IBibliothekService
{
    Task<List<BibliothekDatei>> GetListeAsync(bool istFuehrung, CancellationToken cancellationToken = default);

    /// <summary>Lädt eine Datei in die Bibliothek hoch (Typ-/Größen-Validierung im Storage-Dienst).</summary>
    Task<BibliothekDatei> HochladenAsync(string titel, string? kategorie, bool istVerschlusssache,
        Stream inhalt, string originalName, string contentType, long groesseBytes,
        ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Titel/Kategorie/Verschlusssache nachträglich ändern (VS-Flag nur Führung).</summary>
    Task AktualisierenAsync(string id, string titel, string? kategorie, bool istVerschlusssache,
        ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    Task LoeschenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Für den Download-Endpoint: liefert die Datei nur, wenn sie für den Aufrufer sichtbar ist.</summary>
    Task<BibliothekDatei?> GetFuerDownloadAsync(string id, bool istFuehrung, CancellationToken cancellationToken = default);
}
