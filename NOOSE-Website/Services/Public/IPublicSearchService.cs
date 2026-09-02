using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Services.Public;

/// <summary>Search over what the agency has actually published.</summary>
/// <remarks>
/// A composer, not a query layer. It owns no table, no cache key and no <c>IDbContextFactory</c>; it reads the
/// cached snapshots of the services that do own them, so the suppression belt, the per-surface module switches and
/// the emergency stop are inherited rather than repeated. A second read path over the published tables would have to
/// restate the belt — the trap that was measured once and refused a second time when the hazard list was built.
/// <para>
/// Read-only by construction, like the figures service — but one step further: that one still holds a context for
/// the two tables it counts, and this one holds none, so there is structurally no way past a gate.
/// </para>
/// </remarks>
public interface IPublicSearchService
{
    /// <summary>Hits grouped by published surface; empty while the module is off or the query is too short.</summary>
    Task<PublicSearchResults> SearchAsync(string? query, CancellationToken cancellationToken = default);
}
