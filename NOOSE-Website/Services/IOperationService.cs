using System.Security.Claims;
using NOOSE_Website.Data.Entities.Operations;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Operations;

namespace NOOSE_Website.Services;

/// <summary>
/// Geschäftslogik der Operationen/Einsatzberichte: Liste/Detail (inkl. Verschlusssachen-Filter),
/// Anlegen/Bearbeiten, Papierkorb, Einstufung mit Rang-Gate, beteiligte Agents (mit Ermittlungsleiter)
/// und Historie. Beteiligte Personen/Organisationen laufen über die generische Verknüpfungs-Engine.
/// Alle verändernden Aktionen werden auditiert.
/// </summary>
public interface IOperationService
{
    Task<List<Operation>> GetListAsync(bool isLeadership, CancellationToken cancellationToken = default);
    Task<Operation?> GetDetailAsync(string id, bool isLeadership, CancellationToken cancellationToken = default);
    Task<List<Operation>> GetTrashAsync(CancellationToken cancellationToken = default);
    Task<List<Operation>> SearchAsync(string? searchText, bool isLeadership, int max = 20, CancellationToken cancellationToken = default);

    Task<Operation> CreateAsync(OperationInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task RefreshAsync(string id, OperationInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Einstufung setzen. „Gesichert staatsgefährdend" erfordert Senior Special Agent+ oder Admin.</summary>
    Task ClassificationSetAsync(string id, Classification @new, string? justification, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task<List<ClassificationHistory>> GetClassificationHistoryAsync(string id, bool isLeadership, CancellationToken cancellationToken = default);

    /// <summary>Der Operation zugeteilte (beteiligte) NOOSE-Agents (inkl. Agent-Daten; Ermittlungsleiter zuerst).</summary>
    Task<List<OperationAgent>> GetAgentsAsync(string operationId, CancellationToken cancellationToken = default);

    /// <summary>Die als Ermittlungsleiter markierten Zuteilungen der Operation (inkl. Agent-Daten).</summary>
    Task<List<OperationAgent>> GetInvestigationLeadAsync(string operationId, CancellationToken cancellationToken = default);

    /// <summary>Agent zuteilen. Erlaubt für Führung oder Ermittlungsleiter der Akte; <paramref name="alsErmittlungsleiter"/> nur durch die Führung.</summary>
    Task AgentAllocateAsync(string operationId, string agentId, bool asInvestigationLead, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Zuteilung aufheben. Erlaubt für Führung oder Ermittlungsleiter der Akte.</summary>
    Task AgentRemoveAsync(string allocationId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Ermittlungsleiter-Markierung einer Zuteilung setzen/entfernen – nur Führung.</summary>
    Task InvestigationLeadSetAsync(string allocationId, bool @is, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Audit-Einträge der Operation und ihrer Zuteilungen/Beziehungen (für die Akten-Historie; Verschlusssache-gefiltert).</summary>
    Task<List<AuditLog>> GetHistoryAsync(string operationId, bool isLeadership, CancellationToken cancellationToken = default);
}
