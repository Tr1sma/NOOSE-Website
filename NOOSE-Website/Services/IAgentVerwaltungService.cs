using System.Security.Claims;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>
/// Account-Verwaltung fuer Fuehrung/Admin: Freigabe-Posteingang, Rang-/Rollenvergabe und die
/// Notfall-Sperre (Kill-Switch). Alle veraendernden Aktionen werden protokolliert; Sperren/
/// Entsperren/Rangaenderungen erneuern den SecurityStamp und beenden damit laufende Sitzungen
/// des betroffenen Agents.
/// </summary>
public interface IAgentVerwaltungService
{
    Task<List<Agent>> GetAusstehendeAsync(CancellationToken cancellationToken = default);
    Task<List<Agent>> GetAlleAsync(CancellationToken cancellationToken = default);
    Task<Agent?> FindAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>Ausstehenden Account freischalten und Rang/TRU vergeben → Status Aktiv.</summary>
    Task FreigebenAsync(string agentId, Dienstgrad dienstgrad, bool istTRU, ClaimsPrincipal handelnder);

    /// <summary>Registrierung ablehnen → Status Gesperrt mit Begruendung.</summary>
    Task AblehnenAsync(string agentId, string grund, ClaimsPrincipal handelnder);

    Task RangAendernAsync(string agentId, Dienstgrad dienstgrad, ClaimsPrincipal handelnder);
    Task TruSetzenAsync(string agentId, bool istTRU, ClaimsPrincipal handelnder);
    Task AdminSetzenAsync(string agentId, bool istAdmin, ClaimsPrincipal handelnder);

    /// <summary>Notfall-Sperre: Status Gesperrt + alle Sitzungen sofort beenden (Kill-Switch).</summary>
    Task SperrenAsync(string agentId, string grund, ClaimsPrincipal handelnder);

    /// <summary>Sperre aufheben → Status Aktiv.</summary>
    Task EntsperrenAsync(string agentId, ClaimsPrincipal handelnder);
}
