using System.Security.Claims;
using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Services.Public;

/// <summary>Internal key figures of the public area: does it earn its keep.</summary>
/// <remarks>
/// Read-only by construction, so there is no invalidation to get right — the same sentence the figures service
/// carries, and here too it is a promise a guard holds to. No cache: this is one leadership panel on demand, not a
/// path behind every anonymous page view.
/// <para>No module gate, deliberately. A switched-off module must not hide the history that decides whether to
/// switch it back on.</para>
/// </remarks>
public interface IPublicKpiService
{
    /// <summary>The figures of the last <paramref name="days"/> days.</summary>
    Task<PublicKpiReport> GetAsync(int days, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
