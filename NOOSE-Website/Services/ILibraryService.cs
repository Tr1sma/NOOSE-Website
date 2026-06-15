using System.Security.Claims;
using NOOSE_Website.Data.Entities.Common;

namespace NOOSE_Website.Services;

/// <summary>
/// Zentrale Datei-Bibliothek (Phase 7): durchsuchbare Ablage hochgeladener Dateien (Formulare,
/// SOPs, Vorlagen). Hochladen dürfen alle schreibberechtigten Agenten; Löschen und das
/// Verschlusssache-Flag sind der Führung vorbehalten. Verschlusssachen sieht nur die Führung.
/// </summary>
public interface ILibraryService
{
    Task<List<LibraryFile>> GetListAsync(bool isLeadership, CancellationToken cancellationToken = default);

    /// <summary>Lädt eine Datei in die Bibliothek hoch (Typ-/Größen-Validierung im Storage-Dienst).</summary>
    Task<LibraryFile> UploadAsync(string title, string? category, bool isClassified,
        Stream content, string originalName, string contentType, long sizeBytes,
        ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Titel/Kategorie/Verschlusssache nachträglich ändern (VS-Flag nur Führung).</summary>
    Task RefreshAsync(string id, string title, string? category, bool isClassified,
        ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Für den Download-Endpoint: liefert die Datei nur, wenn sie für den Aufrufer sichtbar ist.</summary>
    Task<LibraryFile?> GetForDownloadAsync(string id, bool isLeadership, CancellationToken cancellationToken = default);
}
