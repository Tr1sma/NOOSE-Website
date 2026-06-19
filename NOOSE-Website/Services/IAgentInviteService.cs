using System.Security.Claims;
using NOOSE_Website.Data.Entities.Recruiting;

namespace NOOSE_Website.Services;

/// <summary>Secret invite links that let a Discord login become a pending agent (bypassing the public application).</summary>
public interface IAgentInviteService
{
    /// <summary>Create a new single-use invite link. Leadership only.</summary>
    Task<AgentInvite> CreateAsync(string? label, DateTime? expiresAt, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Return the invite if the token is valid (exists, not used, not expired), else null. Anonymous-readable.</summary>
    Task<AgentInvite?> ValidateAsync(string? token, CancellationToken cancellationToken = default);

    /// <summary>Mark the invite as used by the given user; throws if already used/invalid.</summary>
    Task ConsumeAsync(string token, string userId, CancellationToken cancellationToken = default);

    /// <summary>For a returning applicant: if the token is valid, transition the user from Applicant to Pending, consume the token and rotate the security stamp. Returns true if redeemed.</summary>
    Task<bool> RedeemForExistingAsync(string? token, string userId, CancellationToken cancellationToken = default);

    /// <summary>Revoke (soft-delete) an invite. Leadership only.</summary>
    Task RevokeAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>All invites (newest first). Leadership only.</summary>
    Task<List<AgentInvite>> ListAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
