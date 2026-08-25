using System.Security.Claims;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Services.Public;

/// <summary>The editorial value list of warning chips a public wanted notice can carry.</summary>
/// <remarks>
/// Deliberately does not touch the assignments: those change what the public sees, so they live on
/// <see cref="IPublicWantedService"/> and go through its one save path, which drops the snapshot.
/// </remarks>
public interface IWarnhinweisService
{
    Task<IReadOnlyList<Warnhinweis>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WarnhinweisUsage>> GetWithUsageAsync(CancellationToken cancellationToken = default);

    /// <summary>What the editor's picker may offer: active rows, in display order.</summary>
    Task<IReadOnlyList<WarnhinweisOption>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<Warnhinweis> CreateAsync(string name, string? colour, int sortOrder, bool isActive, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    Task RefreshAsync(string id, string name, string? colour, int sortOrder, bool isActive, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>Hard delete; the FK cascade clears the assignments, so a live notice loses the chip.</summary>
    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
