using System.Security.Claims;
using NOOSE_Website.Models.Financing;

namespace NOOSE_Website.Services;

/// <summary>Per-rank funding budget rules, stored as JSON in a single system-setting row.</summary>
public interface IFinancingConfigService
{
    /// <summary>Cached rules for the hot read paths.</summary>
    Task<FinancingBudgetConfig> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Always-fresh rules for the settings editor, so it never shows a stale cache.</summary>
    Task<FinancingBudgetConfig> GetEditableAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(FinancingBudgetConfig config, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
