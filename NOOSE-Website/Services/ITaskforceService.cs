using System.Security.Claims;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Taskforces;

namespace NOOSE_Website.Services;

/// <summary>
/// Geschäftslogik der Taskforces (Phase 5c): Liste/Detail (inkl. Verschlusssachen-Filter), Anlegen/Bearbeiten,
/// Papierkorb, Genehmigungs-Status (Führung), zugeteilte Agents inkl. Leitung (Chefermittler/CID-Lead/TRU-Lead)
/// und Historie. Beteiligte Personen/Organisationen laufen über die generische Verknüpfungs-Engine. Eine
/// Taskforce hat – anders als die Verdächtigen-Akten – bewusst keine Einstufung. Alle verändernden Aktionen
/// werden auditiert.
/// </summary>
public interface ITaskforceService
{
    // darfAlles = der Aufrufer darf ALLE Taskforces sehen (Führung/Admin, ClaimsPrincipal.IstFuehrung()).
    // meId = Agent-Id des Aufrufers; sonst sind nur die Taskforces sichtbar, denen er zugeteilt ist.
    Task<List<Taskforce>> GetListAsync(bool mayAll, string? meId, CancellationToken cancellationToken = default);
    Task<Taskforce?> GetDetailAsync(string id, bool mayAll, string? meId, CancellationToken cancellationToken = default);
    Task<List<Taskforce>> GetTrashAsync(CancellationToken cancellationToken = default);
    Task<List<Taskforce>> SearchAsync(string? searchText, bool mayAll, string? meId, int max = 20, CancellationToken cancellationToken = default);

    /// <summary>Beantragte Taskforces (Status = Beantragt) für den Führungs-Freigabe-Posteingang, älteste zuerst.</summary>
    Task<List<Taskforce>> GetRequestedAsync(CancellationToken cancellationToken = default);

    Task<Taskforce> CreateAsync(TaskforceInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task RefreshAsync(string id, TaskforceInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Genehmigungs-Status setzen (z. B. Beantragt → Genehmigt/Abgelehnt/Aufgelöst) – nur Führung.</summary>
    Task ApprovalSetAsync(string id, TaskforceStatus @new, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Der Taskforce zugeteilte NOOSE-Agents (inkl. Agent-Daten; Leitung zuerst).</summary>
    Task<List<TaskforceAgent>> GetAgentsAsync(string taskforceId, CancellationToken cancellationToken = default);

    /// <summary>Die Leitung der Taskforce (Rolle ungleich Mitglied), inkl. Agent-Daten.</summary>
    Task<List<TaskforceAgent>> GetLeadAsync(string taskforceId, CancellationToken cancellationToken = default);

    /// <summary>Agent als Mitglied zuteilen. Erlaubt für Führung oder Leitung dieser Taskforce.</summary>
    Task AgentAllocateAsync(string taskforceId, string agentId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Zuteilung aufheben. Erlaubt für Führung oder Leitung dieser Taskforce.</summary>
    Task AgentRemoveAsync(string allocationId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Rolle einer Zuteilung setzen (Mitglied/Chefermittler/CID-Lead/TRU-Lead) – nur Führung.</summary>
    Task RoleSetAsync(string allocationId, TaskforceRole role, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Audit-Einträge der Taskforce und ihrer Zuteilungen/Beziehungen (für die Akten-Historie; nur sichtbar, wenn zugeteilt oder Führung).</summary>
    Task<List<AuditLog>> GetHistoryAsync(string taskforceId, bool mayAll, string? meId, CancellationToken cancellationToken = default);
}
