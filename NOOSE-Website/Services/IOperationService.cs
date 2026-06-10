using System.Security.Claims;
using NOOSE_Website.Data.Entities.Operationen;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Operationen;

namespace NOOSE_Website.Services;

/// <summary>
/// Geschäftslogik der Operationen/Einsatzberichte: Liste/Detail (inkl. Verschlusssachen-Filter),
/// Anlegen/Bearbeiten, Papierkorb, Einstufung mit Rang-Gate, beteiligte Agents (mit Ermittlungsleiter)
/// und Historie. Beteiligte Personen/Organisationen laufen über die generische Verknüpfungs-Engine.
/// Alle verändernden Aktionen werden auditiert.
/// </summary>
public interface IOperationService
{
    Task<List<Operation>> GetListeAsync(bool istFuehrung, CancellationToken cancellationToken = default);
    Task<Operation?> GetDetailAsync(string id, bool istFuehrung, CancellationToken cancellationToken = default);
    Task<List<Operation>> GetPapierkorbAsync(CancellationToken cancellationToken = default);
    Task<List<Operation>> SucheAsync(string? suchtext, bool istFuehrung, int max = 20, CancellationToken cancellationToken = default);

    Task<Operation> ErstellenAsync(OperationEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task AktualisierenAsync(string id, OperationEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task LoeschenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task WiederherstellenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Einstufung setzen. „Gesichert staatsgefährdend" erfordert Senior Special Agent+ oder Admin.</summary>
    Task EinstufungSetzenAsync(string id, Einstufung neu, string? begruendung, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task<List<EinstufungVerlauf>> GetEinstufungVerlaufAsync(string id, bool istFuehrung, CancellationToken cancellationToken = default);

    /// <summary>Der Operation zugeteilte (beteiligte) NOOSE-Agents (inkl. Agent-Daten; Ermittlungsleiter zuerst).</summary>
    Task<List<OperationAgent>> GetAgentenAsync(string operationId, CancellationToken cancellationToken = default);

    /// <summary>Die als Ermittlungsleiter markierten Zuteilungen der Operation (inkl. Agent-Daten).</summary>
    Task<List<OperationAgent>> GetErmittlungsleiterAsync(string operationId, CancellationToken cancellationToken = default);

    /// <summary>Agent zuteilen. Erlaubt für Führung oder Ermittlungsleiter der Akte; <paramref name="alsErmittlungsleiter"/> nur durch die Führung.</summary>
    Task AgentZuteilenAsync(string operationId, string agentId, bool alsErmittlungsleiter, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Zuteilung aufheben. Erlaubt für Führung oder Ermittlungsleiter der Akte.</summary>
    Task AgentEntfernenAsync(string zuteilungId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Ermittlungsleiter-Markierung einer Zuteilung setzen/entfernen – nur Führung.</summary>
    Task ErmittlungsleiterSetzenAsync(string zuteilungId, bool ist, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Audit-Einträge der Operation und ihrer Zuteilungen/Beziehungen (für die Akten-Historie; Verschlusssache-gefiltert).</summary>
    Task<List<AuditLog>> GetHistorieAsync(string operationId, bool istFuehrung, CancellationToken cancellationToken = default);
}
