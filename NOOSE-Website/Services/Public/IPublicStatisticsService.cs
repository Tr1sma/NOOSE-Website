using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Services.Public;

/// <summary>The public figures of the agency, counted from published rows only.</summary>
/// <remarks>
/// Read-only by construction: there is no write path, so unlike every other public service there is no invalidation
/// to get right — the figures expire and are counted again. Its own module decides whether anything is published at
/// all, and each set of numbers additionally answers to the module that owns the rows it counts.
/// </remarks>
public interface IPublicStatisticsService
{
    /// <summary>Every published figure; <see cref="PublicStatistics.Empty"/> while the module is off.</summary>
    Task<PublicStatistics> GetPublishedAsync(CancellationToken cancellationToken = default);
}
