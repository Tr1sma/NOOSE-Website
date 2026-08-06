using System.Security.Claims;
using NOOSE_Website.Data.Entities.Financing;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Financing;

namespace NOOSE_Website.Services;

/// <summary>Funding requests: requested → approved/rejected → paid, with the payout booked against the treasury.</summary>
public interface IFinancingService
{
    /// <summary>The caller's own requests, newest first.</summary>
    Task<List<FinancingRequestDisplay>> GetOwnAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Requests visible to the caller, optionally filtered by stage.</summary>
    Task<List<FinancingRequestDisplay>> GetVisibleAsync(FinancingStatus? status, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Recent requests of one agent for the personnel file; empty when the caller may not see them.</summary>
    Task<List<FinancingRequestDisplay>> GetForAgentAsync(string agentId, int max, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Requests awaiting a decision (leadership inbox and nav badge).</summary>
    Task<int> GetOpenCountAsync(CancellationToken cancellationToken = default);

    Task<FinancingRequest?> GetAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task<FinancingRequest> CreateAsync(FinancingRequestInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Decides a requested (or previously rejected) request; quantities may be cut per line.</summary>
    Task DecideAsync(string id, bool approved, FinancingDecisionInput decision, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>The requester pulls their own still-open request.</summary>
    Task WithdrawAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Takes an approval back (to requested, or straight to rejected) and frees the reserved budget.</summary>
    Task RevokeApprovalAsync(string id, bool reject, string? note, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Pays the approved subsidy out of the Grüngeld account; booking and request commit together.</summary>
    Task PayAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Reverses a payout: the treasury booking is cancelled and the request goes back to approved.</summary>
    Task CancelPaymentAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task<List<FinancingRequest>> GetTrashAsync(CancellationToken cancellationToken = default);
    Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
