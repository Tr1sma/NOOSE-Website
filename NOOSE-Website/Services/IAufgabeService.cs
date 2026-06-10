using System.Security.Claims;
using NOOSE_Website.Data.Entities.Aufgaben;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Aufgaben;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>
/// Geschäftslogik der Aufgaben/To-Dos – Phase 6. Team-Board: alle aktiven Agenten sehen alle Aufgaben (kein
/// Verschlusssache-/Einstufungs-Konzept). Anlegen mit Mehrfach-Zuweisung, Status/Bearbeiten/Papierkorb,
/// Zuweisen/Entfernen und Historie. Zuweisung und Erledigung erzeugen eine In-App-Benachrichtigung (Glocke).
/// </summary>
public interface IAufgabeService
{
    /// <summary>Team-Board: alle Aufgaben, optional nur eigene (Ersteller ODER zugewiesen). Neueste zuerst.</summary>
    Task<List<AufgabeZeile>> GetTeamboardAsync(bool nurMeine, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task<Aufgabe?> GetDetailAsync(string id, CancellationToken cancellationToken = default);
    Task<List<Aufgabe>> GetPapierkorbAsync(CancellationToken cancellationToken = default);
    Task<List<Aufgabe>> SucheAsync(string? suchtext, int max = 20, CancellationToken cancellationToken = default);

    /// <summary>Legt eine Aufgabe an, weist sie den angegebenen aktiven Agenten zu und benachrichtigt diese (außer den Ersteller).</summary>
    Task<Aufgabe> ErstellenAsync(AufgabeEingabe eingabe, IReadOnlyList<string> agentIds, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    /// <summary>Stammdaten/Status bearbeiten – nur Ersteller oder Führung.</summary>
    Task AktualisierenAsync(string id, AufgabeEingabe eingabe, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    /// <summary>Status setzen – Ersteller, Zugewiesener oder Führung. Bei „Erledigt" wird der Ersteller benachrichtigt.</summary>
    Task StatusSetzenAsync(string id, AufgabeStatus status, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task LoeschenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);
    Task WiederherstellenAsync(string id, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Der Aufgabe zugewiesene Agenten (inkl. Agent-Daten; nach Codename).</summary>
    Task<List<AufgabeZuweisung>> GetZuweisungenAsync(string aufgabeId, CancellationToken cancellationToken = default);

    /// <summary>Agent zuweisen – nur Ersteller oder Führung; benachrichtigt den Agenten (außer er ist der Handelnde).</summary>
    Task AgentZuweisenAsync(string aufgabeId, string agentId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Zuweisung aufheben – nur Ersteller oder Führung.</summary>
    Task AgentEntfernenAsync(string zuweisungId, ClaimsPrincipal handelnder, CancellationToken cancellationToken = default);

    /// <summary>Audit-Einträge der Aufgabe inkl. Zuweisungen/Verknüpfungen (für die Akten-Historie).</summary>
    Task<List<AuditLog>> GetHistorieAsync(string aufgabeId, CancellationToken cancellationToken = default);
}

/// <summary>Listenzeile für das Aufgaben-Team-Board (öffentliche Codenamen, nie Klarname).</summary>
public sealed record AufgabeZeile(
    string Id,
    string Aktenzeichen,
    string Titel,
    AufgabeStatus Status,
    AufgabePrioritaet Prioritaet,
    DateTime? Faelligkeit,
    DateTime? ErledigtAm,
    string? ErstellerCodename,
    IReadOnlyList<string> ZugewieseneCodenames);
