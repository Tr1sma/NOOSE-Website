using System.Security.Claims;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Services.Public;

/// <summary>The money on a head: agency shares from a cash account, private shares from agents, and their coverage.</summary>
/// <remarks>
/// Owns the share table. Everything it writes changes what the outside sees, so every write path ends in
/// <see cref="IPublicWantedService.InvalidatePublicViewAsync"/> — the snapshot's cache key lives in exactly one file
/// and this service is not it.
/// </remarks>
public interface IBountyService
{
    /// <summary>Every share of one notice, newest first; empty when the actor may not read its file.</summary>
    Task<IReadOnlyList<BountyShareRow>> GetSharesAsync(string wantedId, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>The money on one notice, broken down.</summary>
    Task<BountySummary> GetSummaryAsync(string wantedId, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>What each cash account owes in bounties against what it holds; a warning, never a block.</summary>
    Task<IReadOnlyList<BountyCoverage>> GetCoverageAsync(ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>Commit agency money (rank ≥ 3) or file a request for it (rank 1-2).</summary>
    Task<BountyAddOutcome> AddOfficialAsync(string wantedId, decimal amount, KassenKonto account,
        string? justification, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Pledge one's own money; every writing internal agent may.</summary>
    Task AddPrivateAsync(string wantedId, decimal amount, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>Hand a pledged private share in: books one deposit and marks the share secured.</summary>
    Task PayInAsync(string shareId, KassenKonto account, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>Take a share back with a reason; secured money is refused, that is a payout.</summary>
    Task WithdrawAsync(string shareId, string reason, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    // ---- requests ----

    Task<IReadOnlyList<BountyRequestRow>> GetPendingRequestsAsync(CancellationToken cancellationToken = default);

    Task<int> GetPendingRequestCountAsync(CancellationToken cancellationToken = default);

    Task ApproveRequestAsync(string requestId, string? note, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    Task RejectRequestAsync(string requestId, string? note, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);
}
