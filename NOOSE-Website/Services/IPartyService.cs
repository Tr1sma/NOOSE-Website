using System.Security.Claims;
using NOOSE_Website.Data.Entities.Parties;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Parties;

namespace NOOSE_Website.Services;

/// <summary>Business logic for party records: list/detail, CRUD, classification, members, assigned agents, history.</summary>
public interface IPartyService
{
    Task<List<Party>> GetListAsync(ViewerScope scope, CancellationToken cancellationToken = default);
    Task<Party?> GetDetailAsync(string id, ViewerScope scope, CancellationToken cancellationToken = default);
    Task<List<Party>> GetTrashAsync(CancellationToken cancellationToken = default);
    Task<List<Party>> SearchAsync(string? searchText, bool isLeadership, int max = 20, CancellationToken cancellationToken = default);

    Task<Party> CreateAsync(PartyInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task RefreshAsync(string id, PartyInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Set classification. "Secured state-threatening" requires Senior Special Agent+ or Admin.</summary>
    Task ClassificationSetAsync(string id, Classification @new, string? justification, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task<List<ClassificationHistory>> GetClassificationHistoryAsync(string id, ViewerScope scope, CancellationToken cancellationToken = default);

    /// <summary>Party members incl. person; classified persons only for leadership.</summary>
    Task<List<PartyMember>> GetMembersAsync(string partyId, ViewerScope scope, CancellationToken cancellationToken = default);
    Task MemberAddAsync(string partyId, PartyMemberInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task MemberChangeAsync(string memberId, string? role, bool isLead, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task MemberRemoveAsync(string memberId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Agents assigned to the party (incl. agent data; investigation leads first).</summary>
    Task<List<PartyAgent>> GetAgentsAsync(string partyId, CancellationToken cancellationToken = default);

    /// <summary>Assignments marked as investigation lead (incl. agent data).</summary>
    Task<List<PartyAgent>> GetInvestigationLeadAsync(string partyId, CancellationToken cancellationToken = default);

    /// <summary>Assign agent. Leadership or record investigation lead; as-lead flag only by leadership.</summary>
    Task AgentAllocateAsync(string partyId, string agentId, bool asInvestigationLead, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Remove assignment. Leadership or record investigation lead.</summary>
    Task AgentRemoveAsync(string allocationId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Set/clear the investigation-lead mark on an assignment - leadership only.</summary>
    Task InvestigationLeadSetAsync(string allocationId, bool @is, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Audit entries of the party and its memberships (record history; classified-filtered).</summary>
    Task<List<AuditLog>> GetHistoryAsync(string partyId, bool isLeadership, CancellationToken cancellationToken = default);
}
