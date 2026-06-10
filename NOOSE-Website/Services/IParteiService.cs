using System.Security.Claims;
using NOOSE_Website.Data.Entities.Parteien;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Parteien;

namespace NOOSE_Website.Services;

/// <summary>
/// Geschäftslogik der Partei-Akten: Liste/Detail (inkl. Verschlusssachen-Filter), Anlegen/Bearbeiten,
/// Papierkorb, Einstufung mit Rang-Gate, Mitglieder (mit Rolle/Leitung), zugeteilte Agents und Historie.
/// Alle verändernden Aktionen werden auditiert.
/// </summary>
public interface IParteiService
{
    Task<List<Partei>> GetListeAsync(bool istFuehrung, CancellationToken cancellationToken = default);
    Task<Partei?> GetDetailAsync(string id, bool istFuehrung, CancellationToken cancellationToken = default);
    Task<List<Partei>> GetPapierkorbAsync(CancellationToken cancellationToken = default);
    Task<List<Partei>> SucheAsync(string? suchtext, bool istFuehrung, int max = 20, CancellationToken cancellationToken = default);

    Task<Partei> ErstellenAsync(ParteiEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task AktualisierenAsync(string id, ParteiEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task LoeschenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task WiederherstellenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Einstufung setzen. „Gesichert staatsgefährdend" erfordert Senior Special Agent+ oder Admin.</summary>
    Task EinstufungSetzenAsync(string id, Einstufung neu, string? begruendung, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task<List<EinstufungVerlauf>> GetEinstufungVerlaufAsync(string id, bool istFuehrung, CancellationToken cancellationToken = default);

    /// <summary>Mitglieder der Partei inkl. Person; Verschlusssachen-Personen nur für Führung.</summary>
    Task<List<ParteiMitglied>> GetMitgliederAsync(string parteiId, bool istFuehrung, CancellationToken cancellationToken = default);
    Task MitgliedHinzufuegenAsync(string parteiId, ParteiMitgliedEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task MitgliedAendernAsync(string mitgliedId, string? rolle, bool istLeitung, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task MitgliedEntfernenAsync(string mitgliedId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Der Partei zugeteilte NOOSE-Agents (inkl. Agent-Daten).</summary>
    Task<List<ParteiAgent>> GetAgentenAsync(string parteiId, CancellationToken cancellationToken = default);
    Task AgentZuteilenAsync(string parteiId, string agentId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task AgentEntfernenAsync(string zuteilungId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Audit-Einträge der Partei und ihrer Mitgliedschaften (für die Akten-Historie; Verschlusssache-gefiltert).</summary>
    Task<List<AuditLog>> GetHistorieAsync(string parteiId, bool istFuehrung, CancellationToken cancellationToken = default);
}
