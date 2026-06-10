using System.Security.Claims;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Data.Entities.Vorgaenge;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Vorgaenge;

namespace NOOSE_Website.Services;

/// <summary>
/// Geschäftslogik der Vorgangs-/Fallakten: Liste/Detail (inkl. Verschlusssachen-Filter), Anlegen/Bearbeiten,
/// Papierkorb, Einstufung mit Rang-Gate, beteiligte Agents (mit Fallführer) und Historie. Die gebündelten
/// Mitglieder (Personen/Operationen/Observationen/Doks/Organisationen) laufen über die generische
/// Verknüpfungs-Engine. Alle verändernden Aktionen werden auditiert.
/// </summary>
public interface IVorgangService
{
    Task<List<Vorgang>> GetListeAsync(bool istFuehrung, CancellationToken cancellationToken = default);
    Task<Vorgang?> GetDetailAsync(string id, bool istFuehrung, CancellationToken cancellationToken = default);
    Task<List<Vorgang>> GetPapierkorbAsync(CancellationToken cancellationToken = default);
    Task<List<Vorgang>> SucheAsync(string? suchtext, bool istFuehrung, int max = 20, CancellationToken cancellationToken = default);

    Task<Vorgang> ErstellenAsync(VorgangEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task AktualisierenAsync(string id, VorgangEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task LoeschenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task WiederherstellenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Einstufung setzen. „Gesichert staatsgefährdend" erfordert Senior Special Agent+ oder Admin.</summary>
    Task EinstufungSetzenAsync(string id, Einstufung neu, string? begruendung, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task<List<EinstufungVerlauf>> GetEinstufungVerlaufAsync(string id, bool istFuehrung, CancellationToken cancellationToken = default);

    /// <summary>Dem Vorgang zugeteilte (beteiligte) NOOSE-Agents (inkl. Agent-Daten; Fallführer zuerst).</summary>
    Task<List<VorgangAgent>> GetAgentenAsync(string vorgangId, CancellationToken cancellationToken = default);

    /// <summary>Die als Fallführer markierten Zuteilungen des Vorgangs (inkl. Agent-Daten).</summary>
    Task<List<VorgangAgent>> GetFallfuehrerAsync(string vorgangId, CancellationToken cancellationToken = default);

    /// <summary>Agent zuteilen. Erlaubt für Führung oder Fallführer der Akte; <paramref name="alsFallfuehrer"/> nur durch die Führung.</summary>
    Task AgentZuteilenAsync(string vorgangId, string agentId, bool alsFallfuehrer, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Zuteilung aufheben. Erlaubt für Führung oder Fallführer der Akte.</summary>
    Task AgentEntfernenAsync(string zuteilungId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Fallführer-Markierung einer Zuteilung setzen/entfernen – nur Führung.</summary>
    Task FallfuehrerSetzenAsync(string zuteilungId, bool ist, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Audit-Einträge des Vorgangs und seiner Zuteilungen/Verknüpfungen (für die Akten-Historie; Verschlusssache-gefiltert).</summary>
    Task<List<AuditLog>> GetHistorieAsync(string vorgangId, bool istFuehrung, CancellationToken cancellationToken = default);
}
