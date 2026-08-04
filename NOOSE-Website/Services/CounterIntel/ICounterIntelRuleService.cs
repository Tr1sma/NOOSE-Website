using System.Security.Claims;
using NOOSE_Website.Models.CounterIntel;

namespace NOOSE_Website.Services;

/// <summary>CRUD over the leadership-defined counter-intelligence rules. Leadership-only, read-only supervisors excluded.</summary>
public interface ICounterIntelRuleService
{
    /// <summary>Every rule including the inactive ones, in display order.</summary>
    Task<IReadOnlyList<CounterIntelRuleView>> GetAllAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Only the active rules, for evaluation; no actor guard, callers gate the result.</summary>
    Task<IReadOnlyList<CounterIntelRuleView>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<string> CreateAsync(CounterIntelRuleInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task UpdateAsync(string id, CounterIntelRuleInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task SetActiveAsync(string id, bool isActive, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Copies a rule as an inactive draft; returns the new id.</summary>
    Task<string> DuplicateAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Re-creates any built-in rule that no longer exists; returns how many were added.</summary>
    Task<int> RestoreDefaultsAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Every agent selectable in the actor filters.</summary>
    Task<IReadOnlyList<AgentOption>> GetAgentOptionsAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Records whose name or case number matches, for the "specific records" filter.</summary>
    Task<IReadOnlyList<RecordOption>> SearchRecordsAsync(
        ClaimsPrincipal actor, IReadOnlyList<string> entityTypes, string? query, CancellationToken cancellationToken = default);

    /// <summary>Labels for already-picked record ids, so the editor can show names instead of GUIDs.</summary>
    Task<IReadOnlyList<RecordOption>> DescribeRecordsAsync(
        ClaimsPrincipal actor, IReadOnlyList<string> entityIds, CancellationToken cancellationToken = default);
}
