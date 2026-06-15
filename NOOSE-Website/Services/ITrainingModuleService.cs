using System.Security.Claims;
using NOOSE_Website.Data.Entities.Personnel;
using NOOSE_Website.Models.Personnel;

namespace NOOSE_Website.Services;

/// <summary>
/// Ausbildungsmodule (kleine Schulungen) und ihr Abschluss je Agent. Module pflegt der Admin im
/// Verwaltungsbereich; in der Personalakte markiert die Führung Module als abgeschlossen.
/// Folgt dem üblichen Factory-Muster der Dienst-Schicht.
/// </summary>
public interface ITrainingModuleService
{
    // ---- Katalog (Admin) ----
    /// <summary>Alle Module (inkl. inaktiver), nach Sortierung/Name – für die Verwaltung.</summary>
    Task<List<TrainingModule>> GetAllAsync(CancellationToken cancellationToken = default);
    /// <summary>Nur aktive Module, nach Sortierung/Name – für die Personalakte.</summary>
    Task<List<TrainingModule>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<TrainingModule> CreateAsync(ModuleInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task UpdateAsync(string moduleId, ModuleInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task DeleteAsync(string moduleId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    // ---- Abschluss je Agent (Führung) ----
    /// <summary>Alle für die Personalakte relevanten Module samt Abschluss-Status des Agenten (aktive Module
    /// plus bereits abgeschlossene, auch wenn inzwischen inaktiv).</summary>
    Task<List<AgentModuleStatus>> GetStatusForAgentAsync(string agentId, CancellationToken cancellationToken = default);
    /// <summary>Modul für einen Agenten als abgeschlossen markieren – nur Führung; je Agent/Modul nur einmal.</summary>
    Task<AgentModuleCompletion> MarkCompletedAsync(string agentId, string moduleId, string? note, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    /// <summary>Abschluss eines Moduls für einen Agenten entfernen – nur Führung.</summary>
    Task UnmarkCompletedAsync(string completionId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
