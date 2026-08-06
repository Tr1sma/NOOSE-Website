using System.Security.Claims;
using NOOSE_Website.Data.Entities.Financing;
using NOOSE_Website.Models.Financing;

namespace NOOSE_Website.Services;

/// <summary>Per-agent monthly funding budget: rank base plus the previous month's carry-over, minus what is already approved.</summary>
public interface IFinancingBudgetService
{
    /// <summary>Budget of one agent for the running month; closes any elapsed months first.</summary>
    Task<FinancingBudgetStatus> GetStatusAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>Budget of every active internal agent, for the leadership overview.</summary>
    Task<IReadOnlyList<FinancingBudgetStatus>> GetAllStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>Closed periods of one agent, newest first — the audit trail of the carry-over chain.</summary>
    Task<List<FinancingBudgetPeriod>> GetPeriodsAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>Sets or clears (null) an agent's individual monthly budget.</summary>
    Task SetOverrideAsync(string agentId, decimal? amount, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
