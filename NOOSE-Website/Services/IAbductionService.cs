using System.Security.Claims;
using NOOSE_Website.Data.Entities.Abductions;
using NOOSE_Website.Models.Abductions;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Documents abductions of NOOSE agents and the records they compromised.</summary>
public interface IAbductionService
{
    Task<List<AbductionDisplay>> GetListAsync(CancellationToken cancellationToken = default);
    Task<AgentAbduction?> GetAsync(string id, CancellationToken cancellationToken = default);
    Task<AbductionDisplay?> GetDisplayAsync(string id, CancellationToken cancellationToken = default);
    Task<List<AbductionDisplay>> GetForVictimAsync(string agentId, CancellationToken cancellationToken = default);
    Task<List<AbductionDisplay>> GetForPerpetratorAsync(string perpetratorType, string perpetratorId, CancellationToken cancellationToken = default);
    Task<List<AgentAbduction>> GetTrashAsync(CancellationToken cancellationToken = default);

    Task<AgentAbduction> CreateAsync(AbductionInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task UpdateAsync(string id, AbductionInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    // ---- compromised records ----
    Task<AbductionCompromise> AddCompromiseAsync(string abductionId, string targetType, string targetId, string? note, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    /// <summary>Flips a compromise between "compromised" and "re-classified as normal".</summary>
    Task SetCompromiseStatusAsync(string compromiseId, CompromiseStatus status, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task RemoveCompromiseAsync(string compromiseId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    /// <summary>All records compromised by live abductions; activeOnly hides ones re-classified as normal.</summary>
    Task<List<CompromisedRecord>> GetCompromisedRecordsAsync(bool activeOnly = true, CancellationToken cancellationToken = default);
    /// <summary>Compromise entries pointing at one record (both statuses), newest first.</summary>
    Task<List<CompromisedRecord>> GetForTargetAsync(string targetType, string targetId, CancellationToken cancellationToken = default);
    /// <summary>Compromise entries of one abduction (both statuses), newest first.</summary>
    Task<List<CompromisedRecord>> GetCompromisesForAbductionAsync(string abductionId, CancellationToken cancellationToken = default);
    /// <summary>Of the given ids, which are currently compromised (badge lookup).</summary>
    Task<HashSet<string>> GetCompromisedTargetIdsAsync(string targetType, IReadOnlyCollection<string> targetIds, CancellationToken cancellationToken = default);
}
