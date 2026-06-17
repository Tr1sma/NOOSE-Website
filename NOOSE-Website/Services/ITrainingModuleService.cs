using System.Security.Claims;
using NOOSE_Website.Data.Entities.Personnel;
using NOOSE_Website.Models.Personnel;

namespace NOOSE_Website.Services;

/// <summary>Training modules and their per-agent completion.</summary>
public interface ITrainingModuleService
{
    /// <summary>All modules including inactive, ordered by sort/name.</summary>
    Task<List<TrainingModule>> GetAllAsync(CancellationToken cancellationToken = default);
    /// <summary>Active modules only, ordered by sort/name.</summary>
    Task<List<TrainingModule>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<TrainingModule> CreateAsync(ModuleInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task UpdateAsync(string moduleId, ModuleInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task DeleteAsync(string moduleId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Modules relevant to a personnel file with the agent's completion status (active plus already completed).</summary>
    Task<List<AgentModuleStatus>> GetStatusForAgentAsync(string agentId, CancellationToken cancellationToken = default);
    /// <summary>Marks a module completed for an agent; leadership only, once per agent/module.</summary>
    Task<AgentModuleCompletion> MarkCompletedAsync(string agentId, string moduleId, string? note, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    /// <summary>Removes a module completion for an agent; leadership only.</summary>
    Task UnmarkCompletedAsync(string completionId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
