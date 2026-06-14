using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using NOOSE_Website.Infrastructure.CurrentUser;

namespace NOOSE_Website.Infrastructure.Notifications;

/// <summary>
/// Zweiter, vom <c>AuditSaveChangesInterceptor</c> UNABHÄNGIGER Speichervorgang-Interceptor: erkennt zentral jede
/// Änderung an einer folgbaren Akte (über <see cref="WatchlistAkteRollup"/>, inkl. Kind→Eltern-Hochrollung) und
/// stößt nach erfolgreichem Commit den Watchlist-Fan-out an. STRIKT read-only über den ChangeTracker (keine State-/
/// Property-Änderung) – damit der Audit-Interceptor unberührt bleibt.
/// <para>
/// Robust gegen den Re-Entry durch den zweiten SaveChanges des Audit-Interceptors (das Schreiben der AuditLog-Zeilen):
/// dieser Durchlauf enthält nur AuditLog/ZugriffsLog → leere Akten-Menge. Eine leere Menge überschreibt daher NIE eine
/// bereits gemerkte (echte) Menge; verarbeitet (entnommen + versendet) wird in SavedChanges, aufgeräumt in
/// SaveChangesFailed. Dadurch ist das Verhalten unabhängig von der Interceptor-Reihenfolge.
/// </para>
/// Scoped (wie der Audit-Interceptor) – teilt sich mit der DbContext-Factory mehrere kurzlebige Contexts pro Circuit,
/// daher das Pending pro Context in einer <see cref="ConditionalWeakTable{TKey,TValue}"/>.
/// </summary>
public sealed class WatchlistChangeInterceptor(
    ICurrentUserService currentUserService,
    WatchlistDispatcher dispatcher,
    ILogger<WatchlistChangeInterceptor> logger) : SaveChangesInterceptor
{
    private readonly ConditionalWeakTable<DbContext, Pending> _pending = new();

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            var user = await currentUserService.GetAsync();
            Remember(eventData.Context, user.Id);
        }
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            var user = currentUserService.Get();
            Remember(eventData.Context, user.Id);
        }
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Distribute(eventData.Context);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Distribute(eventData.Context);
        return base.SavedChanges(eventData, result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        // Fehlgeschlagener Save → keine Benachrichtigung; das gemerkte Pending verwerfen, damit es nicht beim
        // nächsten (evtl. leeren) Save fälschlich versendet wird.
        if (eventData.Context is not null)
        {
            _pending.Remove(eventData.Context);
        }
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            _pending.Remove(eventData.Context);
        }
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void Remember(DbContext context, string? actorId)
    {
        var records = Collect(context);
        // Eine leere Menge (z. B. der reine AuditLog-Zweitsave) überschreibt eine bereits gemerkte echte Menge NICHT.
        if (records.Count == 0)
        {
            return;
        }
        _pending.AddOrUpdate(context, new Pending(actorId, records));
    }

    private void Distribute(DbContext? context)
    {
        if (context is null || !_pending.TryGetValue(context, out var pending))
        {
            return;
        }
        _pending.Remove(context);
        dispatcher.Distribute(pending.ActorId, pending.Records);
    }

    private HashSet<(string, string)> Collect(DbContext context)
    {
        // Read-only: KEIN State/keine Property ändern. DetectChanges ist idempotent (der Audit-Interceptor ruft es
        // bereits) und stellt sicher, dass Kind-FKs (PersonId, …) auch bei navigations-eingefügten Kindern gesetzt sind.
        context.ChangeTracker.DetectChanges();
        var records = new HashSet<(string, string)>();
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }
            foreach (var record in WatchlistRecordRollup.Map(entry.Entity, logger))
            {
                records.Add(record);
            }
        }
        return records;
    }

    private sealed record Pending(string? ActorId, IReadOnlyCollection<(string Type, string Id)> Records);
}
