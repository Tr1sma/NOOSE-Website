using System.Security.Claims;
using NOOSE_Website.Data.Entities.Parties;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Parties;

namespace NOOSE_Website.Services;

/// <summary>
/// Geschäftslogik der Partei-Akten: Liste/Detail (inkl. Verschlusssachen-Filter), Anlegen/Bearbeiten,
/// Papierkorb, Einstufung mit Rang-Gate, Mitglieder (mit Rolle/Leitung), zugeteilte Agents und Historie.
/// Alle verändernden Aktionen werden auditiert.
/// </summary>
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

    /// <summary>Einstufung setzen. „Gesichert staatsgefährdend" erfordert Senior Special Agent+ oder Admin.</summary>
    Task ClassificationSetAsync(string id, Classification @new, string? justification, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task<List<ClassificationHistory>> GetClassificationHistoryAsync(string id, ViewerScope scope, CancellationToken cancellationToken = default);

    /// <summary>Mitglieder der Partei inkl. Person; Verschlusssachen-Personen nur für Führung.</summary>
    Task<List<PartyMember>> GetMembersAsync(string partyId, ViewerScope scope, CancellationToken cancellationToken = default);
    Task MemberAddAsync(string partyId, PartyMemberInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task MemberChangeAsync(string memberId, string? role, bool isLead, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task MemberRemoveAsync(string memberId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Der Partei zugeteilte NOOSE-Agents (inkl. Agent-Daten; Ermittlungsleiter zuerst).</summary>
    Task<List<PartyAgent>> GetAgentsAsync(string partyId, CancellationToken cancellationToken = default);

    /// <summary>Die als Ermittlungsleiter markierten Zuteilungen der Partei (inkl. Agent-Daten).</summary>
    Task<List<PartyAgent>> GetInvestigationLeadAsync(string partyId, CancellationToken cancellationToken = default);

    /// <summary>Agent zuteilen. Erlaubt für Führung oder Ermittlungsleiter der Akte; <paramref name="alsErmittlungsleiter"/> nur durch die Führung.</summary>
    Task AgentAllocateAsync(string partyId, string agentId, bool asInvestigationLead, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Zuteilung aufheben. Erlaubt für Führung oder Ermittlungsleiter der Akte.</summary>
    Task AgentRemoveAsync(string allocationId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Ermittlungsleiter-Markierung einer Zuteilung setzen/entfernen – nur Führung.</summary>
    Task InvestigationLeadSetAsync(string allocationId, bool @is, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Audit-Einträge der Partei und ihrer Mitgliedschaften (für die Akten-Historie; Verschlusssache-gefiltert).</summary>
    Task<List<AuditLog>> GetHistoryAsync(string partyId, bool isLeadership, CancellationToken cancellationToken = default);
}
