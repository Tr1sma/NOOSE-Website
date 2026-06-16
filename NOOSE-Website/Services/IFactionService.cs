using System.Security.Claims;
using NOOSE_Website.Data.Entities.Factions;
using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Factions;

namespace NOOSE_Website.Services;

/// <summary>
/// Geschäftslogik der Fraktions-Akten: Liste/Detail (inkl. Verschlusssachen-Filter), Anlegen/Bearbeiten
/// mit strukturierten Listen (Ränge/Bestände), Papierkorb, Einstufung mit Rang-Gate, Mitglieder-Pflege
/// (eigene Join-Tabelle mit Rang/Leitung) und Akten-Historie. Alle verändernden Aktionen werden auditiert.
/// </summary>
public interface IFactionService
{
    Task<List<Faction>> GetListAsync(ViewerScope scope, CancellationToken cancellationToken = default);
    Task<Faction?> GetDetailAsync(string id, ViewerScope scope, CancellationToken cancellationToken = default);
    Task<List<Faction>> GetTrashAsync(CancellationToken cancellationToken = default);
    Task<List<Faction>> SearchAsync(string? searchText, bool isLeadership, int max = 20, CancellationToken cancellationToken = default);

    Task<Faction> CreateAsync(FactionInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task RefreshAsync(string id, FactionInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Einstufung setzen. „Gesichert staatsgefährdend" erfordert Senior Special Agent+ oder Admin.</summary>
    Task ClassificationSetAsync(string id, Classification @new, string? justification, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Append-only classification history of the faction, newest first; visibility-filtered.</summary>
    Task<List<ClassificationHistory>> GetClassificationHistoryAsync(string id, ViewerScope scope, CancellationToken cancellationToken = default);

    // Faction members incl. person; classified persons leadership-only, partner-filtered.
    Task<List<FactionMember>> GetMembersAsync(string factionId, ViewerScope scope, CancellationToken cancellationToken = default);
    Task MemberAddAsync(string factionId, MemberInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task MemberChangeAsync(string memberId, string? rank, bool isLead, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task MemberRemoveAsync(string memberId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Der Fraktion zugeteilte NOOSE-Agents (inkl. Agent-Daten; Ermittlungsleiter zuerst).</summary>
    Task<List<FactionAgent>> GetAgentsAsync(string factionId, CancellationToken cancellationToken = default);

    /// <summary>Die als Ermittlungsleiter markierten Zuteilungen der Fraktion (inkl. Agent-Daten).</summary>
    Task<List<FactionAgent>> GetInvestigationLeadAsync(string factionId, CancellationToken cancellationToken = default);

    /// <summary>Agent zuteilen. Erlaubt für Führung oder Ermittlungsleiter der Akte; <paramref name="alsErmittlungsleiter"/> nur durch die Führung.</summary>
    Task AgentAllocateAsync(string factionId, string agentId, bool asInvestigationLead, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Zuteilung aufheben. Erlaubt für Führung oder Ermittlungsleiter der Akte.</summary>
    Task AgentRemoveAsync(string allocationId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Ermittlungsleiter-Markierung einer Zuteilung setzen/entfernen – nur Führung.</summary>
    Task InvestigationLeadSetAsync(string allocationId, bool @is, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Audit-Einträge der Fraktion und ihrer Mitgliedschaften (für die Akten-Historie; Verschlusssache-gefiltert).</summary>
    Task<List<AuditLog>> GetHistoryAsync(string factionId, bool isLeadership, CancellationToken cancellationToken = default);

    // ---- Aktivitäten (Zeitstrahl) ----

    /// <summary>Faction's own activity log, newest first; visibility-filtered, partner-filtered when scope is a partner.</summary>
    Task<List<FactionActivity>> GetActivitiesAsync(string factionId, ViewerScope scope, CancellationToken cancellationToken = default);

    /// <summary>Aktivität hinzufügen (Titel-Pflicht, Verschlusssache-Gate über die Eltern-Fraktion).</summary>
    Task ActivityAddAsync(string factionId, ActivityInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Aktivität ändern (Verschlusssache-Gate über die Eltern-Fraktion).</summary>
    Task ActivityChangeAsync(string activityId, ActivityInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Aktivität entfernen (Soft-Delete; Verschlusssache-Gate über die Eltern-Fraktion).</summary>
    Task ActivityRemoveAsync(string activityId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Bereits genutzte Aktivitäts-Arten (für die Freitext-mit-Vorschlägen-Auswahl im Dialog).</summary>
    Task<List<string>> GetActivityKindsAsync(CancellationToken cancellationToken = default);

    // ---- Fotos (Galerie + Titelbild) ----

    /// <summary>Fotos der Fraktion (Titelbild zuerst, dann nach Aufnahmezeitpunkt). Für die Galerie auf der Detailseite.</summary>
    Task<List<FactionPhoto>> GetPhotosAsync(string factionId, CancellationToken cancellationToken = default);

    /// <summary>Loads a photo with its faction for the protected endpoint, gated to the viewer (partner: child-release gated); null if not visible.</summary>
    Task<FactionPhoto?> GetPhotoWithFactionAsync(string photoId, ViewerScope scope, CancellationToken cancellationToken = default);

    /// <summary>Foto hinzufügen (Typ/Größe serverseitig geprüft). Das erste Foto wird automatisch Titelbild.</summary>
    Task<FactionPhoto> PhotoAddAsync(string factionId, Stream content, string originalName, string contentType, long size, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Foto entfernen (DB-Datensatz zuerst, dann Datei).</summary>
    Task PhotoRemoveAsync(string photoId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Das angegebene Foto als Titelbild markieren; alle anderen Fotos der Fraktion verlieren die Markierung.</summary>
    Task AsTitleImageSetAsync(string photoId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
