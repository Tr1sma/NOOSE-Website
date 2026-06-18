using System.Security.Claims;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Account management for leadership/admin: release inbox, rank/role assignment, kill-switch. Block/unblock/rank changes rotate the SecurityStamp and end the agent's sessions.</summary>
public interface IAgentManagementService
{
    Task<List<Agent>> GetPendingAsync(CancellationToken cancellationToken = default);
    Task<List<Agent>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Active agents without the team-lead marker, sorted by codename; team-leads are never selectable or mentionable.</summary>
    Task<List<Agent>> GetSelectableAsync(CancellationToken cancellationToken = default);

    Task<Agent?> FindAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>Release a pending account and assign rank/TRU/HRB; status becomes Active.</summary>
    Task ReleaseAsync(string agentId, Rank rank, bool isTRU, bool isHRB, ClaimsPrincipal actor);

    /// <summary>Release a pending account as an external partner; no rank or internal flags, read-only on released records.</summary>
    Task ReleaseAsPartnerAsync(string agentId, PartnerAgency agency, PartnerRank partnerRank, ClaimsPrincipal actor);

    /// <summary>Reject a registration; status becomes Blocked with a reason.</summary>
    Task RejectAsync(string agentId, string reason, ClaimsPrincipal actor);

    /// <summary>Set an agent's master data; codename is required.</summary>
    Task MasterDataChangeAsync(string agentId, string? realName, string codename, string? badgeNumber, ClaimsPrincipal actor);

    /// <summary>Request a self master-data change (below Supervisory): staged until leadership approves; codename required. A repeat call overwrites a pending request.</summary>
    Task NameChangeRequestAsync(string agentId, string? realName, string codename, string? badgeNumber, ClaimsPrincipal actor);

    /// <summary>Pending name-change requests for the release inbox.</summary>
    Task<List<Agent>> GetPendingNameChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Approve the pending name-change request; applies staged values and ends sessions.</summary>
    Task NameChangeApproveAsync(string agentId, ClaimsPrincipal actor);

    /// <summary>Reject the pending name-change request; staged values are discarded.</summary>
    Task NameChangeRejectAsync(string agentId, string reason, ClaimsPrincipal actor);

    Task RankChangeAsync(string agentId, Rank rank, ClaimsPrincipal actor);

    /// <summary>Change a partner account's partner rank; refreshes the claims.</summary>
    Task SetPartnerRankAsync(string agentId, PartnerRank partnerRank, ClaimsPrincipal actor);

    /// <summary>Switch an active account to a partner agency (also used to change the agency of an existing
    /// partner). Clears the internal rank and TRU/HRB/team-lead/admin flags; forces re-login.</summary>
    Task ConvertToPartnerAsync(string agentId, PartnerAgency agency, PartnerRank partnerRank, ClaimsPrincipal actor);

    /// <summary>Switch an active partner account back to an internal NOOSE agent with the given rank; forces re-login.</summary>
    Task ConvertToInternalAsync(string agentId, Rank rank, ClaimsPrincipal actor);

    /// <summary>Decide a promotion request (Deputy Director+/Admin); on approval sets the rank, logs it, and rotates the SecurityStamp.</summary>
    Task PromotionDecideAsync(string requestId, bool approved, string? note, ClaimsPrincipal actor);

    Task TruSetAsync(string agentId, bool isTRU, ClaimsPrincipal actor);
    Task HrbSetAsync(string agentId, bool isHRB, ClaimsPrincipal actor);
    Task AdminSetAsync(string agentId, bool isAdmin, ClaimsPrincipal actor);

    /// <summary>Mark/unmark an agent as FiveM team-lead; visibility marker only, grants no rights.</summary>
    Task TeamLeadSetAsync(string agentId, bool isTeamLead, ClaimsPrincipal actor);

    /// <summary>Kill-switch: status Blocked and end all sessions immediately.</summary>
    Task BlockAsync(string agentId, string reason, ClaimsPrincipal actor);

    /// <summary>Lift a block; status becomes Active.</summary>
    Task UnblockAsync(string agentId, ClaimsPrincipal actor);
}
