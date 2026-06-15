using System.Security.Claims;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <summary>Gespeicherte Suchen/Smart-Listen je Agent (anlegen, eigene laden, eigene löschen).</summary>
public interface ISavedSearchService
{
    Task<List<SavedSearch>> GetForAgentAsync(string agentId, CancellationToken cancellationToken = default);

    Task<SavedSearch> SaveAsync(string agentId, string name, SearchCriteria criteria, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, string agentId, CancellationToken cancellationToken = default);
}
