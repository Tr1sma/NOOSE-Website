using System.Security.Claims;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Common;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>
/// Gesetzbuch/Rechtsgrundlagen-Modul (Phase 7): von der Führung kuratierte Paragrafen, für alle
/// aktiven Agenten lesbar und über die Verknüpfungs-Engine mit Akten (Vorgängen, Doks …) verknüpfbar.
/// </summary>
public interface ILawService
{
    Task<List<Law>> GetListAsync(CancellationToken cancellationToken = default, PartnerAgency? partnerAgency = null, string? partnerAgentId = null);
    Task<Law?> GetAsync(string id, CancellationToken cancellationToken = default, PartnerAgency? partnerAgency = null, string? partnerAgentId = null);

    /// <summary>Suche nach Gesetzbuch/Paragraf/Titel (für Autocomplete, z. B. im Verknüpfen-Dialog).</summary>
    Task<List<Law>> SearchAsync(string? searchText, int max = 20, CancellationToken cancellationToken = default);

    Task<Law> CreateAsync(LawInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task RefreshAsync(string id, LawInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
