using System.Security.Claims;

namespace NOOSE_Website.Services;

/// <summary>Idempotently seeds the demo agent and example records for the public demo instance. Admin only.</summary>
public interface IDemoDataService
{
    /// <summary>Seeds missing demo data; returns the number of newly added rows.</summary>
    Task<int> SeedAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
