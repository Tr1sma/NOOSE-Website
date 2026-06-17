using System.Security.Claims;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Common;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Law/legal-basis module: leadership-curated paragraphs, readable by all active agents and linkable to records.</summary>
public interface ILawService
{
    Task<List<Law>> GetListAsync(CancellationToken cancellationToken = default, PartnerAgency? partnerAgency = null, string? partnerAgentId = null);
    Task<Law?> GetAsync(string id, CancellationToken cancellationToken = default, PartnerAgency? partnerAgency = null, string? partnerAgentId = null);

    /// <summary>Search by code/paragraph/title for autocomplete.</summary>
    Task<List<Law>> SearchAsync(string? searchText, int max = 20, CancellationToken cancellationToken = default);

    Task<Law> CreateAsync(LawInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task RefreshAsync(string id, LawInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
