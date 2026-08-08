using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NOOSE_Website.Data.Entities.Search;
using NOOSE_Website.Services;

namespace NOOSE_Website.Infrastructure.Search;

/// <summary>Rebuilds the phonetic/stem search side-index for changed records inside the same SaveChanges
/// (registered LAST, after the audit/soft-delete interceptor, so it reads final field state). Delete-by-SourceId
/// then re-insert. Soft-deleted rows are re-indexed on purpose — hits resolve against the live-filtered table.</summary>
public sealed class SearchIndexInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } ctx)
        {
            var (delete, insert) = Plan(ctx);
            if (delete.Count > 0)
            {
                var oldKeys = ctx.Set<SearchPhoneticKey>().Where(r => delete.Contains(r.SourceId)).ToList();
                var oldStems = ctx.Set<SearchStemToken>().Where(r => delete.Contains(r.SourceId)).ToList();
                ctx.Set<SearchPhoneticKey>().RemoveRange(oldKeys);
                ctx.Set<SearchStemToken>().RemoveRange(oldStems);
                AddRows(ctx, insert);
            }
        }
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } ctx)
        {
            var (delete, insert) = Plan(ctx);
            if (delete.Count > 0)
            {
                var oldKeys = await ctx.Set<SearchPhoneticKey>().Where(r => delete.Contains(r.SourceId)).ToListAsync(cancellationToken);
                var oldStems = await ctx.Set<SearchStemToken>().Where(r => delete.Contains(r.SourceId)).ToListAsync(cancellationToken);
                ctx.Set<SearchPhoneticKey>().RemoveRange(oldKeys);
                ctx.Set<SearchStemToken>().RemoveRange(oldStems);
                AddRows(ctx, insert);
            }
        }
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    // sourceIds to purge (any change) + rows to (re)insert (Added/Modified only)
    private static (HashSet<string> Delete, List<SearchIndexRow> Insert) Plan(DbContext ctx)
    {
        ctx.ChangeTracker.DetectChanges();
        var delete = new HashSet<string>(StringComparer.Ordinal);
        var insert = new List<SearchIndexRow>();
        foreach (var entry in ctx.ChangeTracker.Entries().ToList())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }
            if (SearchIndexProjection.For(entry.Entity) is not { } row)
            {
                continue;
            }
            delete.Add(row.SourceId);
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                insert.Add(row);
            }
        }
        return (delete, insert);
    }

    private static void AddRows(DbContext ctx, List<SearchIndexRow> insert)
    {
        foreach (var row in insert)
        {
            foreach (var key in row.PhoneticKeys)
            {
                ctx.Add(new SearchPhoneticKey { EntityType = row.EntityType, EntityId = row.EntityId, SourceId = row.SourceId, Key = key });
            }
            foreach (var stem in row.Stems)
            {
                ctx.Add(new SearchStemToken { EntityType = row.EntityType, EntityId = row.EntityId, SourceId = row.SourceId, Stem = stem });
            }
        }
    }
}
