using System.Security.Claims;
using NOOSE_Website.Models.Organigramm;

namespace NOOSE_Website.Services;

/// <summary>
/// Stellt die NOOSE-interne Struktur für die Organigramm-Seite zusammen: aktive Agenten nach Dienstgrad,
/// TRU-Querschnitt und die für den Betrachter sichtbaren, genehmigten Taskforces mit Besetzung. Rein lesend.
/// </summary>
public interface IOrganigrammService
{
    Task<OrganigrammDaten> GetAsync(ClaimsPrincipal betrachter, CancellationToken cancellationToken = default);
}
