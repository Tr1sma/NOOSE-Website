using System.Security.Claims;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Data.Entities.Cases;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Cases;

namespace NOOSE_Website.Services;

/// <summary>
/// Geschäftslogik der Vorgangs-/Fallakten: Liste/Detail (inkl. Verschlusssachen-Filter), Anlegen/Bearbeiten,
/// Papierkorb, Einstufung mit Rang-Gate, beteiligte Agents (mit Fallführer) und Historie. Die gebündelten
/// Mitglieder (Personen/Operationen/Observationen/Doks/Organisationen) laufen über die generische
/// Verknüpfungs-Engine. Alle verändernden Aktionen werden auditiert.
/// </summary>
public interface ICaseService
{
    Task<List<Case>> GetListAsync(ViewerScope scope, CancellationToken cancellationToken = default);
    Task<Case?> GetDetailAsync(string id, ViewerScope scope, CancellationToken cancellationToken = default);
    Task<List<Case>> GetTrashAsync(CancellationToken cancellationToken = default);
    Task<List<Case>> SearchAsync(string? searchText, bool isLeadership, int max = 20, CancellationToken cancellationToken = default);

    Task<Case> CreateAsync(CaseInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task RefreshAsync(string id, CaseInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Einstufung setzen. „Gesichert staatsgefährdend" erfordert Senior Special Agent+ oder Admin.</summary>
    Task ClassificationSetAsync(string id, Classification @new, string? justification, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task<List<ClassificationHistory>> GetClassificationHistoryAsync(string id, ViewerScope scope, CancellationToken cancellationToken = default);

    /// <summary>Dem Vorgang zugeteilte (beteiligte) NOOSE-Agents (inkl. Agent-Daten; Fallführer zuerst).</summary>
    Task<List<CaseAgent>> GetAgentsAsync(string caseId, CancellationToken cancellationToken = default);

    /// <summary>Die als Fallführer markierten Zuteilungen des Vorgangs (inkl. Agent-Daten).</summary>
    Task<List<CaseAgent>> GetCaseLeadAsync(string caseId, CancellationToken cancellationToken = default);

    /// <summary>Agent zuteilen. Erlaubt für Führung oder Fallführer der Akte; <paramref name="alsFallfuehrer"/> nur durch die Führung.</summary>
    Task AgentAllocateAsync(string caseId, string agentId, bool asCaseLead, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Zuteilung aufheben. Erlaubt für Führung oder Fallführer der Akte.</summary>
    Task AgentRemoveAsync(string allocationId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Fallführer-Markierung einer Zuteilung setzen/entfernen – nur Führung.</summary>
    Task CaseLeadSetAsync(string allocationId, bool @is, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Audit-Einträge des Vorgangs und seiner Zuteilungen/Verknüpfungen (für die Akten-Historie; Verschlusssache-gefiltert).</summary>
    Task<List<AuditLog>> GetHistoryAsync(string caseId, bool isLeadership, CancellationToken cancellationToken = default);
}
