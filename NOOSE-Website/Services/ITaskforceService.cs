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
    Task<List<Taskforce>> GetListeAsync(bool istFuehrung, CancellationToken cancellationToken = default);
    Task<Taskforce?> GetDetailAsync(string id, bool istFuehrung, CancellationToken cancellationToken = default);
    Task<List<Taskforce>> GetPapierkorbAsync(CancellationToken cancellationToken = default);
    Task<List<Taskforce>> SucheAsync(string? suchtext, bool istFuehrung, int max = 20, CancellationToken cancellationToken = default);

    /// <summary>Beantragte Taskforces (Status = Beantragt) für den Führungs-Freigabe-Posteingang, älteste zuerst.</summary>
    Task<List<Taskforce>> GetBeantragteAsync(CancellationToken cancellationToken = default);

    Task<Taskforce> ErstellenAsync(TaskforceEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task AktualisierenAsync(string id, TaskforceEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task LoeschenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task WiederherstellenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Genehmigungs-Status setzen (z. B. Beantragt → Genehmigt/Abgelehnt/Aufgelöst) – nur Führung.</summary>
    Task GenehmigungSetzenAsync(string id, TaskforceStatus neu, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Der Taskforce zugeteilte NOOSE-Agents (inkl. Agent-Daten; Leitung zuerst).</summary>
    Task<List<TaskforceAgent>> GetAgentenAsync(string taskforceId, CancellationToken cancellationToken = default);

    /// <summary>Die Leitung der Taskforce (Rolle ungleich Mitglied), inkl. Agent-Daten.</summary>
    Task<List<TaskforceAgent>> GetLeitungAsync(string taskforceId, CancellationToken cancellationToken = default);

    /// <summary>Agent als Mitglied zuteilen. Erlaubt für Führung oder Leitung dieser Taskforce.</summary>
    Task AgentZuteilenAsync(string taskforceId, string agentId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Zuteilung aufheben. Erlaubt für Führung oder Leitung dieser Taskforce.</summary>
    Task AgentEntfernenAsync(string zuteilungId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Rolle einer Zuteilung setzen (Mitglied/Chefermittler/CID-Lead/TRU-Lead) – nur Führung.</summary>
    Task RolleSetzenAsync(string zuteilungId, TaskforceRolle rolle, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Audit-Einträge der Taskforce und ihrer Zuteilungen/Beziehungen (für die Akten-Historie; Verschlusssache-gefiltert).</summary>
    Task<List<AuditLog>> GetHistorieAsync(string taskforceId, bool istFuehrung, CancellationToken cancellationToken = default);
}
