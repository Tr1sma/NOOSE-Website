using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NOOSE_Website.Data;
using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Services.Public;

/// <inheritdoc cref="IPublicStatisticsService" />
public class PublicStatisticsService(
    IDbContextFactory<AppDbContext> dbFactory,
    IPublicModuleService modules,
    IPublicWantedService wanted,
    IMemoryCache cache) : IPublicStatisticsService
{
    private const string CacheKey = "OeffentlicheZahlen";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(10);

    /// <summary>The counted rows, cached; the module switches are read outside and decide what is shown.</summary>
    private sealed record Counts(int TipsReceived, int TipsConfirmed, int TipsLedToCapture, decimal RewardsPaid);

    public async Task<PublicStatistics> GetPublishedAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await modules.GetAsync(cancellationToken);
        if (!snapshot.IsEnabled(PublicModules.Statistics))
        {
            return PublicStatistics.Empty;
        }

        // Asked here as well, although both wanted reads gate themselves: they answer an off module with an empty
        // board, and "0" is a different statement from "we do not publish this". The gate is what turns the one
        // into the other.
        int? open = snapshot.IsEnabled(PublicModules.Wanted)
            ? (await wanted.GetBoardAsync(cancellationToken)).Cards.Count
            : null;
        // the captures come from the same snapshot rather than a query of their own, which would have to repeat the
        // suppression belt; the archive list is capped for page weight and this figure deliberately is not
        int? captured = snapshot.IsEnabled(PublicModules.WantedArchive)
            ? await wanted.GetCapturedTotalAsync(cancellationToken)
            : null;

        var counts = await LoadAsync(cancellationToken);
        // an unreachable database publishes no figure rather than a zero, exactly like the situation level
        var tips = counts is not null && snapshot.IsEnabled(PublicModules.Tips) ? counts : null;
        var rewards = counts is not null && snapshot.IsEnabled(PublicModules.Reward) ? counts : null;

        return new PublicStatistics(open, captured,
            tips?.TipsReceived, tips?.TipsConfirmed, tips?.TipsLedToCapture, rewards?.RewardsPaid);
    }

    /// <summary>Counts the two tables this service owns figures for; null when they cannot be read.</summary>
    private async Task<Counts?> LoadAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CacheKey, out Counts? cached) && cached is not null)
        {
            return cached;
        }

        Counts counts;
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            // Four counts rather than one grouped projection, because that is what it takes to name the shared
            // predicates instead of writing them again here: TipRules owns what "confirmed" means, and a second
            // copy would drift the day a status is added. Soft-deleted tips fall out through the query filter —
            // a submission the agency removed is not a submission it received.
            var received = await db.Hinweise.AsNoTracking().CountAsync(cancellationToken);
            var confirmed = await db.Hinweise.AsNoTracking().CountAsync(TipRules.ConfirmedRows, cancellationToken);
            var ledToCapture = await db.Hinweise.AsNoTracking().CountAsync(TipRules.CaptureRows, cancellationToken);
            // no soft-delete filter to weaken here: money history is append-only, so every row is a real payout
            var paid = await db.HinweisBelohnungen.AsNoTracking().SumAsync(r => r.Amount, cancellationToken);

            counts = new Counts(received, confirmed, ledToCapture, paid);
        }
        catch (Exception)
        {
            // never cache a failure: the next visitor should count again rather than read a hole for ten seconds
            return null;
        }

        cache.Set(CacheKey, counts, CacheDuration);
        return counts;
    }
}
