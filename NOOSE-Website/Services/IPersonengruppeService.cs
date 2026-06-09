using System.Security.Claims;
using NOOSE_Website.Data.Entities.Gruppen;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Gruppen;

namespace NOOSE_Website.Services;

/// <summary>
/// Geschäftslogik der Personengruppen-Akten: Liste/Detail (inkl. Verschlusssachen-Filter), Anlegen/
/// Bearbeiten, Papierkorb, Einstufung mit Rang-Gate, Mitglieder (mit Rolle/Leitung), zugeteilte Agents,
/// Erfassungsfortschritt und Historie. Alle verändernden Aktionen werden auditiert.
/// </summary>
public interface IPersonengruppeService
{
    Task<List<Personengruppe>> GetListeAsync(bool istFuehrung, CancellationToken cancellationToken = default);
    Task<Personengruppe?> GetDetailAsync(string id, bool istFuehrung, CancellationToken cancellationToken = default);
    Task<List<Personengruppe>> GetPapierkorbAsync(CancellationToken cancellationToken = default);
    Task<List<Personengruppe>> SucheAsync(string? suchtext, bool istFuehrung, int max = 20, CancellationToken cancellationToken = default);

    Task<Personengruppe> ErstellenAsync(PersonengruppeEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task AktualisierenAsync(string id, PersonengruppeEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task LoeschenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task WiederherstellenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Einstufung setzen. „Gesichert staatsgefährdend" erfordert Senior Special Agent+ oder Admin.</summary>
    Task EinstufungSetzenAsync(string id, Einstufung neu, string? begruendung, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task<List<EinstufungVerlauf>> GetEinstufungVerlaufAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Mitglieder der Gruppe inkl. Person; Verschlusssachen-Personen nur für Führung.</summary>
    Task<List<PersonengruppeMitglied>> GetMitgliederAsync(string gruppeId, bool istFuehrung, CancellationToken cancellationToken = default);
    Task MitgliedHinzufuegenAsync(string gruppeId, GruppeMitgliedEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task MitgliedAendernAsync(string mitgliedId, string? rolle, bool istLeitung, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task MitgliedEntfernenAsync(string mitgliedId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Der Gruppe zugeteilte NOOSE-Agents (inkl. Agent-Daten).</summary>
    Task<List<PersonengruppeAgent>> GetAgentenAsync(string gruppeId, CancellationToken cancellationToken = default);
    Task AgentZuteilenAsync(string gruppeId, string agentId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task AgentEntfernenAsync(string zuteilungId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Erfassungsfortschritt x/y (x = erfasste Mitglieder mit lebender Akte, y = geschätzte Größe).</summary>
    Task<PersonengruppeFortschritt> GetFortschrittAsync(string gruppeId, CancellationToken cancellationToken = default);

    /// <summary>Audit-Einträge der Gruppe und ihrer Mitgliedschaften (für die Akten-Historie).</summary>
    Task<List<AuditLog>> GetHistorieAsync(string gruppeId, CancellationToken cancellationToken = default);
}
