using System.Security.Claims;
using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Services.Public;

/// <summary>The paragraphs the agency released to the outside world.</summary>
/// <remarks>
/// Its own service although it is thin, and it reads the table itself rather than going through
/// <see cref="ILawService"/>: a public read path never borrows an internal list service, because the internal one
/// answers a different question and may widen at any time without anybody thinking about the outside.
/// </remarks>
public interface IPublicLawService
{
    /// <summary>Every released paragraph, grouped by law book and cached; empty while the module is off.</summary>
    Task<PublicLawSnapshot> GetPublishedAsync(CancellationToken cancellationToken = default);

    /// <summary>Every paragraph with its release flag, for the settings panel.</summary>
    Task<IReadOnlyList<LawReleaseRow>> GetAllAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>The one write path of the release flag.</summary>
    Task SetPublicAsync(string lawId, bool isPublic, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Drops the public snapshot after somebody else wrote the law table.</summary>
    /// <remarks>
    /// Called by <see cref="ILawService"/> after every write: a released paragraph that was edited or deleted would
    /// otherwise stand outside for a whole cache window. Same shape as the bounty path into the wanted snapshot.
    /// </remarks>
    Task InvalidatePublicViewAsync();
}
