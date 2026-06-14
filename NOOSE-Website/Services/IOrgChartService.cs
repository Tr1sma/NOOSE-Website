using System.Security.Claims;
using NOOSE_Website.Models.OrgChart;

namespace NOOSE_Website.Services;

/// <summary>
/// Stellt die NOOSE-interne Struktur für die Organigramm-Seite zusammen: aktive Agenten nach Dienstgrad,
/// TRU-/HRB-Querschnitt und die für den Betrachter sichtbaren, genehmigten Taskforces mit Besetzung. Rein lesend.
/// </summary>
public interface IOrgChartService
{
    Task<OrgChartData> GetAsync(ClaimsPrincipal viewer, CancellationToken cancellationToken = default);
}
