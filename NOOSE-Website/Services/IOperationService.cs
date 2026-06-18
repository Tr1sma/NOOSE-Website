using System.Security.Claims;
using NOOSE_Website.Data.Entities.Operations;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Operations;

namespace NOOSE_Website.Services;

/// <summary>Operations/mission reports: list/detail, CRUD, classification, involved agents and history.</summary>
public interface IOperationService
{
    Task<List<Operation>> GetListAsync(ViewerScope scope, CancellationToken cancellationToken = default);
    Task<Operation?> GetDetailAsync(string id, ViewerScope scope, CancellationToken cancellationToken = default);
    Task<List<Operation>> GetTrashAsync(CancellationToken cancellationToken = default);
    Task<List<Operation>> SearchAsync(string? searchText, bool isLeadership, int max = 20, CancellationToken cancellationToken = default);

    Task<Operation> CreateAsync(OperationInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task RefreshAsync(string id, OperationInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Set classification; "Gesichert staatsgefährdend" requires Senior Special Agent+ or Admin.</summary>
    Task ClassificationSetAsync(string id, Classification @new, string? justification, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task<List<ClassificationHistory>> GetClassificationHistoryAsync(string id, ViewerScope scope, CancellationToken cancellationToken = default);

    /// <summary>NOOSE agents involved in the operation (investigation leads first).</summary>
    Task<List<OperationAgent>> GetAgentsAsync(string operationId, CancellationToken cancellationToken = default);

    /// <summary>Allocations marked as investigation lead.</summary>
    Task<List<OperationAgent>> GetInvestigationLeadAsync(string operationId, CancellationToken cancellationToken = default);

    /// <summary>Allocate an agent; lead flag is leadership-only.</summary>
    Task AgentAllocateAsync(string operationId, string agentId, bool asInvestigationLead, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task AgentRemoveAsync(string allocationId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Set/clear the investigation-lead flag; leadership only.</summary>
    Task InvestigationLeadSetAsync(string allocationId, bool @is, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Audit entries for the operation and its allocations/relations; visibility-filtered.</summary>
    Task<List<AuditLog>> GetHistoryAsync(string operationId, bool isLeadership, CancellationToken cancellationToken = default);
}
