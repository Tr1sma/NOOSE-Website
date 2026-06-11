using System.Security.Claims;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>
/// Account-Verwaltung für Führung/Admin: Freigabe-Posteingang, Rang-/Rollenvergabe und die
/// Notfall-Sperre (Kill-Switch). Alle verändernden Aktionen werden protokolliert; Sperren/
/// Entsperren/Rangänderungen erneuern den SecurityStamp und beenden damit laufende Sitzungen
/// des betroffenen Agents.
/// </summary>
public interface IAgentVerwaltungService
{
    Task<List<Agent>> GetAusstehendeAsync(CancellationToken cancellationToken = default);
    Task<List<Agent>> GetAlleAsync(CancellationToken cancellationToken = default);

    /// <summary>Für Auswahl-/Zuteilungs-/Erwähnungslisten: nur aktive Agenten OHNE TeamLeitung-Marker
    /// (sortiert nach Codename). TeamLeitungen sollen im RP-Betrieb nirgends auswählbar oder erwähnbar sein.</summary>
    Task<List<Agent>> GetAuswaehlbareAsync(CancellationToken cancellationToken = default);

    Task<Agent?> FindAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>Ausstehenden Account freischalten und Rang/TRU vergeben → Status Aktiv.</summary>
    Task FreigebenAsync(string agentId, Dienstgrad dienstgrad, bool istTRU, ClaimsPrincipal handelnder);

    /// <summary>Registrierung ablehnen → Status Gesperrt mit Begründung.</summary>
    Task AblehnenAsync(string agentId, string grund, ClaimsPrincipal handelnder);

    /// <summary>Stammdaten (Klarname, Codename, Dienstnummer) eines Agents setzen. Codename ist Pflicht.</summary>
    Task StammdatenAendernAsync(string agentId, string? klarname, string codename, string? dienstnummer, ClaimsPrincipal handelnder);

    /// <summary>
    /// Beantragt eine Selbst-Stammdatenänderung für Ränge unterhalb Supervisory: Der gewünschte Zielzustand
    /// wird zwischengelagert (Live-Daten bleiben unverändert), bis die Führung ihn freigibt. Codename ist Pflicht.
    /// Ein erneuter Aufruf überschreibt einen offenen Antrag.
    /// </summary>
    Task NamensaenderungBeantragenAsync(string agentId, string? klarname, string codename, string? dienstnummer, ClaimsPrincipal handelnder);

    /// <summary>Offene Namensänderungs-Anträge (für den Freigabe-Posteingang).</summary>
    Task<List<Agent>> GetAusstehendeNamensaenderungenAsync(CancellationToken cancellationToken = default);

    /// <summary>Genehmigt den offenen Namensänderungs-Antrag: beantragte Werte werden übernommen, Sitzungen enden.</summary>
    Task NamensaenderungGenehmigenAsync(string agentId, ClaimsPrincipal handelnder);

    /// <summary>Lehnt den offenen Namensänderungs-Antrag ab: Pending-Felder werden verworfen, Live-Daten bleiben.</summary>
    Task NamensaenderungAblehnenAsync(string agentId, string grund, ClaimsPrincipal handelnder);

    Task RangAendernAsync(string agentId, Dienstgrad dienstgrad, ClaimsPrincipal handelnder);

    /// <summary>Entscheidet über einen Beförderungsantrag (Deputy Director+/Admin). Bei Genehmigung wird der
    /// Rang gesetzt, im Dienstgrad-Verlauf protokolliert und der SecurityStamp erneuert.</summary>
    Task BefoerderungEntscheidenAsync(string antragId, bool genehmigt, string? notiz, ClaimsPrincipal handelnder);

    Task TruSetzenAsync(string agentId, bool istTRU, ClaimsPrincipal handelnder);
    Task AdminSetzenAsync(string agentId, bool istAdmin, ClaimsPrincipal handelnder);

    /// <summary>Markiert/entmarkiert einen Agenten als FiveM-Teamleitung. Reiner Sichtbarkeits-Marker –
    /// vergibt keine Rechte; Vollzugriff wird separat über <see cref="AdminSetzenAsync"/> gesetzt.</summary>
    Task TeamLeitungSetzenAsync(string agentId, bool istTeamLeitung, ClaimsPrincipal handelnder);

    /// <summary>Notfall-Sperre: Status Gesperrt + alle Sitzungen sofort beenden (Kill-Switch).</summary>
    Task SperrenAsync(string agentId, string grund, ClaimsPrincipal handelnder);

    /// <summary>Sperre aufheben → Status Aktiv.</summary>
    Task EntsperrenAsync(string agentId, ClaimsPrincipal handelnder);
}
