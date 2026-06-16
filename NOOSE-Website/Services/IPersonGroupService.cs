using System.Security.Claims;
using NOOSE_Website.Data.Entities.Groups;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Groups;

namespace NOOSE_Website.Services;

/// <summary>
/// Geschäftslogik der Personengruppen-Akten: Liste/Detail (inkl. Verschlusssachen-Filter), Anlegen/
/// Bearbeiten, Papierkorb, Einstufung mit Rang-Gate, Mitglieder (mit Rolle/Leitung), zugeteilte Agents,
/// Erfassungsfortschritt und Historie. Alle verändernden Aktionen werden auditiert.
/// </summary>
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

    /// <summary>Einstufung setzen. „Gesichert staatsgefährdend" erfordert Senior Special Agent+ oder Admin.</summary>
    Task ClassificationSetAsync(string id, Classification @new, string? justification, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task<List<ClassificationHistory>> GetClassificationHistoryAsync(string id, ViewerScope scope, CancellationToken cancellationToken = default);

    /// <summary>Mitglieder der Gruppe inkl. Person; Verschlusssachen-Personen nur für Führung.</summary>
    Task<List<PersonGroupMember>> GetMembersAsync(string groupId, ViewerScope scope, CancellationToken cancellationToken = default);
    Task MemberAddAsync(string groupId, GroupMemberInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task MemberChangeAsync(string memberId, string? role, bool isLead, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task MemberRemoveAsync(string memberId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Der Gruppe zugeteilte NOOSE-Agents (inkl. Agent-Daten; Ermittlungsleiter zuerst).</summary>
    Task<List<PersonGroupAgent>> GetAgentsAsync(string groupId, CancellationToken cancellationToken = default);

    /// <summary>Die als Ermittlungsleiter markierten Zuteilungen der Gruppe (inkl. Agent-Daten).</summary>
    Task<List<PersonGroupAgent>> GetInvestigationLeadAsync(string groupId, CancellationToken cancellationToken = default);

    /// <summary>Agent zuteilen. Erlaubt für Führung oder Ermittlungsleiter der Akte; <paramref name="alsErmittlungsleiter"/> nur durch die Führung.</summary>
    Task AgentAllocateAsync(string groupId, string agentId, bool asInvestigationLead, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Zuteilung aufheben. Erlaubt für Führung oder Ermittlungsleiter der Akte.</summary>
    Task AgentRemoveAsync(string allocationId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Ermittlungsleiter-Markierung einer Zuteilung setzen/entfernen – nur Führung.</summary>
    Task InvestigationLeadSetAsync(string allocationId, bool @is, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Erfassungsfortschritt x/y (x = erfasste Mitglieder mit lebender Akte, y = geschätzte Größe).</summary>
    Task<PersonGroupProgress> GetProgressAsync(string groupId, CancellationToken cancellationToken = default);

    /// <summary>Audit-Einträge der Gruppe und ihrer Mitgliedschaften (für die Akten-Historie; Verschlusssache-gefiltert).</summary>
    Task<List<AuditLog>> GetHistoryAsync(string groupId, bool isLeadership, CancellationToken cancellationToken = default);
}
