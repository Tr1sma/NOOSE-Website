using System.Security.Claims;
using NOOSE_Website.Data.Entities.Groups;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Groups;

namespace NOOSE_Website.Services;

/// <summary>Business logic for person-group records: list/detail, CRUD, classification, members, assigned agents, progress, history.</summary>
public interface IPersonGroupService
{
    Task<List<PersonGroup>> GetListAsync(ViewerScope scope, CancellationToken cancellationToken = default);
    Task<PersonGroup?> GetDetailAsync(string id, ViewerScope scope, CancellationToken cancellationToken = default);
    Task<List<PersonGroup>> GetTrashAsync(CancellationToken cancellationToken = default);
    Task<List<PersonGroup>> SearchAsync(string? searchText, bool isLeadership, int max = 20, CancellationToken cancellationToken = default);

    Task<PersonGroup> CreateAsync(PersonGroupInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task RefreshAsync(string id, PersonGroupInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Set classification. "Secured state-threatening" requires Senior Special Agent+ or Admin.</summary>
    Task ClassificationSetAsync(string id, Classification @new, string? justification, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task<List<ClassificationHistory>> GetClassificationHistoryAsync(string id, ViewerScope scope, CancellationToken cancellationToken = default);

    /// <summary>Group members incl. person; classified persons only for leadership.</summary>
    Task<List<PersonGroupMember>> GetMembersAsync(string groupId, ViewerScope scope, CancellationToken cancellationToken = default);
    Task MemberAddAsync(string groupId, GroupMemberInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task MemberChangeAsync(string memberId, string? role, bool isLead, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task MemberRemoveAsync(string memberId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Agents assigned to the group (incl. agent data; investigation leads first).</summary>
    Task<List<PersonGroupAgent>> GetAgentsAsync(string groupId, CancellationToken cancellationToken = default);

    /// <summary>Assignments marked as investigation lead (incl. agent data).</summary>
    Task<List<PersonGroupAgent>> GetInvestigationLeadAsync(string groupId, CancellationToken cancellationToken = default);

    /// <summary>Assign agent. Leadership or record investigation lead; as-lead flag only by leadership.</summary>
    Task AgentAllocateAsync(string groupId, string agentId, bool asInvestigationLead, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Remove assignment. Leadership or record investigation lead.</summary>
    Task AgentRemoveAsync(string allocationId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Set/clear the investigation-lead mark on an assignment - leadership only.</summary>
    Task InvestigationLeadSetAsync(string allocationId, bool @is, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Capture progress x/y (x = recorded members with a live record, y = estimated size).</summary>
    Task<PersonGroupProgress> GetProgressAsync(string groupId, CancellationToken cancellationToken = default);

    /// <summary>Audit entries of the group and its memberships (record history; classified-filtered).</summary>
    Task<List<AuditLog>> GetHistoryAsync(string groupId, bool isLeadership, CancellationToken cancellationToken = default);
}
