using System.Security.Claims;

namespace NOOSE_Website.Services;

/// <summary>Loads and saves the admin-configurable threat score configuration: code default fallback, DB overlay, 10-minute cache, leadership-gated save.</summary>
public interface IThreatScoreConfigService
{
    /// <summary>Current configuration for the calculation (cached); callers must NOT mutate the instance.</summary>
    Task<ThreatScoreConfiguration> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Fresh mutable copy for admin editing (not cached).</summary>
    Task<ThreatScoreConfiguration> GetEditableAsync(CancellationToken cancellationToken = default);

    /// <summary>Validates and saves the configuration (leadership required) and invalidates the cache.</summary>
    Task SaveAsync(ThreatScoreConfiguration config, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
