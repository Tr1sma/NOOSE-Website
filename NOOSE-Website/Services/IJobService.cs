using System.Security.Claims;
using NOOSE_Website.Data.Entities.Jobs;
using NOOSE_Website.Infrastructure.Audit;
using NOOSE_Website.Models.Jobs;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>
/// Geschäftslogik der Aufgaben/To-Dos – Phase 6. Team-Board: alle aktiven Agenten sehen alle Aufgaben (kein
/// Verschlusssache-/Einstufungs-Konzept). Anlegen mit Mehrfach-Zuweisung, Status/Bearbeiten/Papierkorb,
/// Zuweisen/Entfernen und Historie. Zuweisung und Erledigung erzeugen eine In-App-Benachrichtigung (Glocke).
/// </summary>
public interface IJobService
{
    /// <summary>Team-Board: alle Aufgaben, optional nur eigene (Ersteller ODER zugewiesen). Neueste zuerst.</summary>
    Task<List<JobRow>> GetTeamBoardAsync(bool onlyMy, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    /// <summary>Lädt eine Aufgabe – liefert null, wenn sie eingeschränkt und für den Aufrufer nicht sichtbar ist.</summary>
    Task<Job?> GetDetailAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task<List<Job>> GetTrashAsync(CancellationToken cancellationToken = default);
    /// <summary>Aufgaben-Suche für Picker; eingeschränkte Aufgaben nur für Beteiligte/Aufsicht (<paramref name="darfAlles"/> = DarfVerschlusssacheLesen).</summary>
    Task<List<Job>> SearchAsync(string? searchText, bool mayAll, string? meId, int max = 20, CancellationToken cancellationToken = default);

    /// <summary>Legt eine Aufgabe an, weist sie den angegebenen aktiven Agenten zu und benachrichtigt diese (außer den Ersteller).</summary>
    Task<Job> CreateAsync(JobInput input, IReadOnlyList<string> agentIds, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    /// <summary>Stammdaten/Status bearbeiten – nur Ersteller oder Führung.</summary>
    Task RefreshAsync(string id, JobInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    /// <summary>Status setzen – Ersteller, Zugewiesener oder Führung. Bei „Erledigt" wird der Ersteller benachrichtigt.</summary>
    Task StatusSetAsync(string id, JobStatus status, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Der Aufgabe zugewiesene Agenten (inkl. Agent-Daten; nach Codename).</summary>
    Task<List<JobAssignment>> GetAssignmentsAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>Agent zuweisen – nur Ersteller oder Führung; benachrichtigt den Agenten (außer er ist der Handelnde).</summary>
    Task AgentAssignAsync(string jobId, string agentId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Zuweisung aufheben – nur Ersteller oder Führung.</summary>
    Task AgentRemoveAsync(string assignmentId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Audit-Einträge der Aufgabe inkl. Zuweisungen/Verknüpfungen (für die Akten-Historie).</summary>
    Task<List<AuditLog>> GetHistoryAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>Öffentlicher Anzeigename einer Bezugs-Akte (für den vorausgefüllten Bezug beim Anlegen), sofern für den Aufrufer sichtbar; sonst null. Nie Klarname.</summary>
    Task<string?> ReferenceDisplayAsync(string entityType, string entityId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}

/// <summary>
/// Listenzeile/Karte für das Aufgaben-Team-Board (öffentliche Codenamen, nie Klarname).
/// Klasse (nicht Record), damit das Kanban-Board den <see cref="Status"/> beim Drag&amp;Drop optimistisch
/// umsetzen kann. <see cref="DarfStatusAendern"/> spiegelt das Server-Gate (Ersteller/Zugewiesener/Führung).
/// </summary>
public sealed class JobRow
{
    public string Id { get; set; } = string.Empty;
    public string CaseNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public JobStatus Status { get; set; }
    public JobPriority Priority { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? DoneAt { get; set; }
    public string? CreatorCodename { get; set; }
    public IReadOnlyList<string> AssignedCodenames { get; set; } = Array.Empty<string>();
    public bool MayStatusChange { get; set; }
}
