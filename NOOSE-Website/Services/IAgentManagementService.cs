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
public interface IAgentManagementService
{
    Task<List<Agent>> GetPendingAsync(CancellationToken cancellationToken = default);
    Task<List<Agent>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Für Auswahl-/Zuteilungs-/Erwähnungslisten: nur aktive Agenten OHNE TeamLeitung-Marker
    /// (sortiert nach Codename). TeamLeitungen sollen im RP-Betrieb nirgends auswählbar oder erwähnbar sein.</summary>
    Task<List<Agent>> GetSelectableAsync(CancellationToken cancellationToken = default);

    Task<Agent?> FindAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>Ausstehenden Account freischalten und Rang/TRU/HRB vergeben → Status Aktiv.</summary>
    Task ReleaseAsync(string agentId, Rank rank, bool isTRU, bool isHRB, ClaimsPrincipal actor);

    /// <summary>Ausstehenden Account als externen Partner (DoJ/LSPD/LSMD) freischalten → Status Aktiv, kein Rang,
    /// keine internen Flags. Partner haben nur Lesezugriff auf freigegebene Akten.</summary>
    Task ReleaseAsPartnerAsync(string agentId, PartnerAgency agency, PartnerRank partnerRank, ClaimsPrincipal actor);

    /// <summary>Registrierung ablehnen → Status Gesperrt mit Begründung.</summary>
    Task RejectAsync(string agentId, string reason, ClaimsPrincipal actor);

    /// <summary>Stammdaten (Klarname, Codename, Dienstnummer) eines Agents setzen. Codename ist Pflicht.</summary>
    Task MasterDataChangeAsync(string agentId, string? realName, string codename, string? badgeNumber, ClaimsPrincipal actor);

    /// <summary>
    /// Beantragt eine Selbst-Stammdatenänderung für Ränge unterhalb Supervisory: Der gewünschte Zielzustand
    /// wird zwischengelagert (Live-Daten bleiben unverändert), bis die Führung ihn freigibt. Codename ist Pflicht.
    /// Ein erneuter Aufruf überschreibt einen offenen Antrag.
    /// </summary>
    Task NameChangeRequestAsync(string agentId, string? realName, string codename, string? badgeNumber, ClaimsPrincipal actor);

    /// <summary>Offene Namensänderungs-Anträge (für den Freigabe-Posteingang).</summary>
    Task<List<Agent>> GetPendingNameChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Genehmigt den offenen Namensänderungs-Antrag: beantragte Werte werden übernommen, Sitzungen enden.</summary>
    Task NameChangeApproveAsync(string agentId, ClaimsPrincipal actor);

    /// <summary>Lehnt den offenen Namensänderungs-Antrag ab: Pending-Felder werden verworfen, Live-Daten bleiben.</summary>
    Task NameChangeRejectAsync(string agentId, string reason, ClaimsPrincipal actor);

    Task RankChangeAsync(string agentId, Rank rank, ClaimsPrincipal actor);

    /// <summary>Ändert den Partner-Rang eines Partner-Kontos (Member/Special/Chief). Erneuert die Claims.</summary>
    Task SetPartnerRankAsync(string agentId, PartnerRank partnerRank, ClaimsPrincipal actor);

    /// <summary>Switch an active account to a partner agency (also used to change the agency of an existing
    /// partner). Clears the internal rank and TRU/HRB/team-lead/admin flags; forces re-login.</summary>
    Task ConvertToPartnerAsync(string agentId, PartnerAgency agency, PartnerRank partnerRank, ClaimsPrincipal actor);

    /// <summary>Switch an active partner account back to an internal NOOSE agent with the given rank; forces re-login.</summary>
    Task ConvertToInternalAsync(string agentId, Rank rank, ClaimsPrincipal actor);

    /// <summary>Entscheidet über einen Beförderungsantrag (Deputy Director+/Admin). Bei Genehmigung wird der
    /// Rang gesetzt, im Dienstgrad-Verlauf protokolliert und der SecurityStamp erneuert.</summary>
    Task PromotionDecideAsync(string requestId, bool approved, string? note, ClaimsPrincipal actor);

    Task TruSetAsync(string agentId, bool isTRU, ClaimsPrincipal actor);
    Task HrbSetAsync(string agentId, bool isHRB, ClaimsPrincipal actor);
    Task AdminSetAsync(string agentId, bool isAdmin, ClaimsPrincipal actor);

    /// <summary>Markiert/entmarkiert einen Agenten als FiveM-Teamleitung. Reiner Sichtbarkeits-Marker –
    /// vergibt keine Rechte; Vollzugriff wird separat über <see cref="AdminSetzenAsync"/> gesetzt.</summary>
    Task TeamLeadSetAsync(string agentId, bool isTeamLead, ClaimsPrincipal actor);

    /// <summary>Notfall-Sperre: Status Gesperrt + alle Sitzungen sofort beenden (Kill-Switch).</summary>
    Task BlockAsync(string agentId, string reason, ClaimsPrincipal actor);

    /// <summary>Sperre aufheben → Status Aktiv.</summary>
    Task UnblockAsync(string agentId, ClaimsPrincipal actor);
}
