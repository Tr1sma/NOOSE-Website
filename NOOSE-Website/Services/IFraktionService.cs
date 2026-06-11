using System.Security.Claims;
using NOOSE_Website.Data.Entities.Fraktionen;
using NOOSE_Website.Data.Entities.Personen;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Fraktionen;

namespace NOOSE_Website.Services;

/// <summary>
/// Geschäftslogik der Fraktions-Akten: Liste/Detail (inkl. Verschlusssachen-Filter), Anlegen/Bearbeiten
/// mit strukturierten Listen (Ränge/Bestände), Papierkorb, Einstufung mit Rang-Gate, Mitglieder-Pflege
/// (eigene Join-Tabelle mit Rang/Leitung) und Akten-Historie. Alle verändernden Aktionen werden auditiert.
/// </summary>
public interface IFraktionService
{
    Task<List<Fraktion>> GetListeAsync(bool istFuehrung, CancellationToken cancellationToken = default);
    Task<Fraktion?> GetDetailAsync(string id, bool istFuehrung, CancellationToken cancellationToken = default);
    Task<List<Fraktion>> GetPapierkorbAsync(CancellationToken cancellationToken = default);
    Task<List<Fraktion>> SucheAsync(string? suchtext, bool istFuehrung, int max = 20, CancellationToken cancellationToken = default);

    Task<Fraktion> ErstellenAsync(FraktionEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task AktualisierenAsync(string id, FraktionEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task LoeschenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task WiederherstellenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Einstufung setzen. „Gesichert staatsgefährdend" erfordert Senior Special Agent+ oder Admin.</summary>
    Task EinstufungSetzenAsync(string id, Einstufung neu, string? begruendung, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Append-only Einstufungs-Verlauf der Fraktion (neueste zuerst; Verschlusssache-gefiltert).</summary>
    Task<List<EinstufungVerlauf>> GetEinstufungVerlaufAsync(string id, bool istFuehrung, CancellationToken cancellationToken = default);

    /// <summary>Mitglieder der Fraktion inkl. Person; Verschlusssachen-Personen nur für Führung.</summary>
    Task<List<FraktionMitglied>> GetMitgliederAsync(string fraktionId, bool istFuehrung, CancellationToken cancellationToken = default);
    Task MitgliedHinzufuegenAsync(string fraktionId, MitgliedEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task MitgliedAendernAsync(string mitgliedId, string? rang, bool istLeitung, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task MitgliedEntfernenAsync(string mitgliedId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Der Fraktion zugeteilte NOOSE-Agents (inkl. Agent-Daten; Ermittlungsleiter zuerst).</summary>
    Task<List<FraktionAgent>> GetAgentenAsync(string fraktionId, CancellationToken cancellationToken = default);

    /// <summary>Die als Ermittlungsleiter markierten Zuteilungen der Fraktion (inkl. Agent-Daten).</summary>
    Task<List<FraktionAgent>> GetErmittlungsleiterAsync(string fraktionId, CancellationToken cancellationToken = default);

    /// <summary>Agent zuteilen. Erlaubt für Führung oder Ermittlungsleiter der Akte; <paramref name="alsErmittlungsleiter"/> nur durch die Führung.</summary>
    Task AgentZuteilenAsync(string fraktionId, string agentId, bool alsErmittlungsleiter, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Zuteilung aufheben. Erlaubt für Führung oder Ermittlungsleiter der Akte.</summary>
    Task AgentEntfernenAsync(string zuteilungId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Ermittlungsleiter-Markierung einer Zuteilung setzen/entfernen – nur Führung.</summary>
    Task ErmittlungsleiterSetzenAsync(string zuteilungId, bool ist, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Audit-Einträge der Fraktion und ihrer Mitgliedschaften (für die Akten-Historie; Verschlusssache-gefiltert).</summary>
    Task<List<AuditLog>> GetHistorieAsync(string fraktionId, bool istFuehrung, CancellationToken cancellationToken = default);

    // ---- Aktivitäten (Zeitstrahl) ----

    /// <summary>Aktivitäten der Fraktion für den Zeitstrahl (neueste zuerst). Verschlusssache nur für Führung.</summary>
    Task<List<FraktionAktivitaet>> GetAktivitaetenAsync(string fraktionId, bool istFuehrung, CancellationToken cancellationToken = default);

    /// <summary>Aktivität hinzufügen (Titel-Pflicht, Verschlusssache-Gate über die Eltern-Fraktion).</summary>
    Task AktivitaetHinzufuegenAsync(string fraktionId, AktivitaetEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Aktivität ändern (Verschlusssache-Gate über die Eltern-Fraktion).</summary>
    Task AktivitaetAendernAsync(string aktivitaetId, AktivitaetEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Aktivität entfernen (Soft-Delete; Verschlusssache-Gate über die Eltern-Fraktion).</summary>
    Task AktivitaetEntfernenAsync(string aktivitaetId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Bereits genutzte Aktivitäts-Arten (für die Freitext-mit-Vorschlägen-Auswahl im Dialog).</summary>
    Task<List<string>> GetAktivitaetArtenAsync(CancellationToken cancellationToken = default);

    // ---- Fotos (Galerie + Titelbild) ----

    /// <summary>Fotos der Fraktion (Titelbild zuerst, dann nach Aufnahmezeitpunkt). Für die Galerie auf der Detailseite.</summary>
    Task<List<FraktionFoto>> GetFotosAsync(string fraktionId, CancellationToken cancellationToken = default);

    /// <summary>Ein Foto inkl. Fraktion (für den geschützten Auslieferungs-Endpoint).</summary>
    Task<FraktionFoto?> GetFotoMitFraktionAsync(string fotoId, CancellationToken cancellationToken = default);

    /// <summary>Foto hinzufügen (Typ/Größe serverseitig geprüft). Das erste Foto wird automatisch Titelbild.</summary>
    Task<FraktionFoto> FotoHinzufuegenAsync(string fraktionId, Stream inhalt, string originalName, string contentType, long groesse, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Foto entfernen (DB-Datensatz zuerst, dann Datei).</summary>
    Task FotoEntfernenAsync(string fotoId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Das angegebene Foto als Titelbild markieren; alle anderen Fotos der Fraktion verlieren die Markierung.</summary>
    Task AlsTitelbildSetzenAsync(string fotoId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
}
