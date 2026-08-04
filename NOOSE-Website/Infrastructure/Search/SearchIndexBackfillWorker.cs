using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Common;
using NOOSE_Website.Data.Entities.Search;
using NOOSE_Website.Models.Common;
using NOOSE_Website.Services;

namespace NOOSE_Website.Infrastructure.Search;

/// <summary>One-shot startup backfill: builds the phonetic/stem side-index for all existing records when the index
/// is still empty (fresh deploy). Ongoing changes are handled by <see cref="SearchIndexInterceptor"/>.</summary>
public sealed class SearchIndexBackfillWorker(IServiceScopeFactory scopeFactory, ILogger<SearchIndexBackfillWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using var db = await factory.CreateDbContextAsync(stoppingToken);

            // guard on a completion marker, not table emptiness: a mid-backfill crash or a concurrent live edit in the
            // startup window would otherwise leave a non-empty table that permanently skips the rest of the corpus
            if (await db.SystemSettings.AnyAsync(s => s.Key == SystemSettingKeys.SearchIndexBackfillDone, stoppingToken))
            {
                return; // already completed — never re-scan on later starts
            }

            // wipe any partial rows (from a prior aborted run or interceptor writes during the delay) and rebuild
            // from scratch so the pass is idempotent; the records themselves are re-indexed below
            await db.SearchPhoneticKeys.ExecuteDeleteAsync(stoppingToken);
            await db.SearchStemTokens.ExecuteDeleteAsync(stoppingToken);

            var total = 0;
            total += await IndexAllAsync(db, db.People, stoppingToken);
            total += await IndexAllAsync(db, db.PersonAliases, stoppingToken);
            total += await IndexAllAsync(db, db.Factions, stoppingToken);
            total += await IndexAllAsync(db, db.PersonGroups, stoppingToken);
            total += await IndexAllAsync(db, db.Parties, stoppingToken);
            total += await IndexAllAsync(db, db.Operations, stoppingToken);
            total += await IndexAllAsync(db, db.Taskforces, stoppingToken);
            total += await IndexAllAsync(db, db.Cases, stoppingToken);
            total += await IndexAllAsync(db, db.Jobs, stoppingToken);

            // mark done only after every type is indexed, so an abort before here re-runs the whole pass next start
            db.SystemSettings.Add(new SystemSetting { Key = SystemSettingKeys.SearchIndexBackfillDone, Value = "true" });
            await db.SaveChangesAsync(stoppingToken);

            if (total > 0)
            {
                logger.LogInformation("Search index backfill: {Count} records indexed.", total);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            /* shutting down */
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Search index backfill failed.");
        }
    }

    // index one entity set; the interceptor ignores the index rows we add (not an indexed type), so this is a plain insert
    private static async Task<int> IndexAllAsync<T>(AppDbContext db, IQueryable<T> set, CancellationToken ct) where T : class
    {
        var items = await set.AsNoTracking().ToListAsync(ct);
        foreach (var item in items)
        {
            if (SearchIndexProjection.For(item) is not { } row)
            {
                continue;
            }
            foreach (var key in row.PhoneticKeys)
            {
                db.Add(new SearchPhoneticKey { EntityType = row.EntityType, EntityId = row.EntityId, SourceId = row.SourceId, Key = key });
            }
            foreach (var stem in row.Stems)
            {
                db.Add(new SearchStemToken { EntityType = row.EntityType, EntityId = row.EntityId, SourceId = row.SourceId, Stem = stem });
            }
        }
        if (items.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }
        return items.Count;
    }
}
