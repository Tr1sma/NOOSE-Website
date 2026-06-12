using System.Security.Claims;
using NOOSE_Website.Data.Entities.Querschnitt;
using NOOSE_Website.Models.Querschnitt;

namespace NOOSE_Website.Services;

/// <summary>
/// Gesetzbuch/Rechtsgrundlagen-Modul (Phase 7): von der Führung kuratierte Paragrafen, für alle
/// aktiven Agenten lesbar und über die Verknüpfungs-Engine mit Akten (Vorgängen, Doks …) verknüpfbar.
/// </summary>
public interface IGesetzService
{
    Task<List<Gesetz>> GetListeAsync(CancellationToken cancellationToken = default);
    Task<Gesetz?> GetAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Suche nach Gesetzbuch/Paragraf/Titel (für Autocomplete, z. B. im Verknüpfen-Dialog).</summary>
    Task<List<Gesetz>> SucheAsync(string? suchtext, int max = 20, CancellationToken cancellationToken = default);

    Task<Gesetz> ErstellenAsync(GesetzEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task AktualisierenAsync(string id, GesetzEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task LoeschenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
}
