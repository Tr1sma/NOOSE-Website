using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>The one rule for what counts as active stock, and the one way to move a record in or out of the archive.</summary>
/// <remarks>
/// Deliberately not a global query filter like <see cref="ISoftDelete"/>. Archiving is soft: the detail page, the
/// link panel, mention resolution and the graph keep showing the record, so the filter is applied where a listing
/// enumerates the stock and nowhere else. Never hand-roll the predicate - same rule as AgentSelection.
/// </remarks>
public static class RecordArchive
{
    /// <summary>Active stock: everything not filed away.</summary>
    public static IQueryable<T> OnlyActive<T>(this IQueryable<T> query) where T : class, IArchivable
        => query.Where(x => !x.IsArchived);

    /// <summary>The archive itself.</summary>
    public static IQueryable<T> OnlyArchived<T>(this IQueryable<T> query) where T : class, IArchivable
        => query.Where(x => x.IsArchived);

    /// <summary>The part of the stock a listing asked for.</summary>
    public static IQueryable<T> Apply<T>(this IQueryable<T> query, ArchiveFilter filter) where T : class, IArchivable
        => filter switch
        {
            ArchiveFilter.Active => query.OnlyActive(),
            ArchiveFilter.Only => query.OnlyArchived(),
            _ => query,
        };

    /// <summary>Moves one record in or out of the archive; false when it already was in that state.</summary>
    /// <remarks>
    /// ExecuteUpdate on purpose: a tracked save would stamp GeaendertAm through the audit interceptor, and a record
    /// coming back out of the archive would then read as freshly edited - which the recency traffic light believes.
    /// Callers own the write guard and the audit row, because this bypasses both interceptors.
    /// </remarks>
    public static async Task<bool> SetArchivedAsync<T>(
        AppDbContext db, string id, bool archived, string? reason, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
        where T : class, IArchivable
    {
        // locals so EF parameterizes instead of baking the values into the SQL
        var stamp = archived ? DateTime.UtcNow : (DateTime?)null;
        var meId = archived ? actor.GetAgentId() : null;
        var note = archived && !string.IsNullOrWhiteSpace(reason) ? reason.Trim() : null;

        // IArchivable carries no key, so the id goes through EF's property accessor
        var changed = await db.Set<T>()
            .Where(x => EF.Property<string>(x, "Id") == id && x.IsArchived != archived)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IsArchived, archived)
                .SetProperty(x => x.ArchivedAt, stamp)
                .SetProperty(x => x.ArchivedById, meId)
                .SetProperty(x => x.ArchiveReason, note), cancellationToken);
        return changed > 0;
    }
}
