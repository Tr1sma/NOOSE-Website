using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NOOSE_Website.Infrastructure.CurrentUser;
using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Infrastructure.Audit;

/// <summary>
/// Querschnitts-Interceptor für alle Speichervorgänge:
/// <list type="bullet">
///   <item>stempelt <see cref="IAuditable"/>-Entitäten (ErstelltAm/Von, GeaendertAm/Von),</item>
///   <item>wandelt physisches Löschen von <see cref="ISoftDelete"/>-Entitäten in Soft-Delete um,</item>
///   <item>schreibt einen <see cref="AuditLog"/>-Eintrag pro betroffener Akte (mit final aufgelöstem
///   Schlüssel, daher zweiphasig: sammeln in <c>SavingChanges</c>, schreiben in <c>SavedChanges</c>).</item>
/// </list>
/// Greift produktiv ab Phase 2, sobald Akten diese Interfaces implementieren. In Phase 1 existieren
/// noch keine auditierbaren Entitäten – das Gerüst ist aber vollständig verdrahtet.
/// </summary>
public class AuditSaveChangesInterceptor(ICurrentUserService currentUserService) : SaveChangesInterceptor
{
    // Mit der DbContext-Factory teilt sich ein (scoped) Interceptor mehrere kurzlebige Contexts pro
    // Circuit. Die noch ausstehenden Audit-Einträge daher pro Context halten, statt in einem Feld –
    // sonst überschreiben sich gleichzeitige Speichervorgänge gegenseitig.
    private readonly ConditionalWeakTable<DbContext, List<PendingAudit>> _pending = new();

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            var user = await currentUserService.GetAsync();
            _pending.AddOrUpdate(eventData.Context, StampAndCollect(eventData.Context, user));
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            // Synchroner Pfad: den Agent synchron (HttpContext-only) ermitteln, statt blockierend auf den
            // async AuthenticationStateProvider zu warten (Deadlock-/Starvation-Risiko).
            var user = currentUserService.Get();
            _pending.AddOrUpdate(eventData.Context, StampAndCollect(eventData.Context, user));
        }

        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        await WritePendingAsync(eventData.Context, cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        // Synchroner Pfad: Audit-Einträge ebenfalls synchron schreiben (kein Blocking auf async SaveChanges).
        WritePending(eventData.Context);
        return base.SavedChanges(eventData, result);
    }

    private async Task WritePendingAsync(DbContext? context, CancellationToken cancellationToken)
    {
        if (!TryTakePending(context, out var ctx, out var logs))
        {
            return;
        }
        ctx.Set<AuditLog>().AddRange(logs);
        // Erneutes Speichern; löst keine Rekursion aus, da AuditLog weder IAuditable noch ISoftDelete ist.
        await ctx.SaveChangesAsync(cancellationToken);
    }

    private void WritePending(DbContext? context)
    {
        if (!TryTakePending(context, out var ctx, out var logs))
        {
            return;
        }
        ctx.Set<AuditLog>().AddRange(logs);
        ctx.SaveChanges();
    }

    /// <summary>
    /// Entnimmt die ausstehenden Audit-Einträge eines Contexts (einmalig). Pro Context nur einmal verarbeitet –
    /// auch der re-entrante AuditLog-Save landet hier (dann mit leerer Liste) und wird so sauber abgeräumt.
    /// </summary>
    private bool TryTakePending(DbContext? context, out DbContext ctx, out List<AuditLog> logs)
    {
        ctx = context!;
        logs = new List<AuditLog>();
        if (context is null || !_pending.TryGetValue(context, out var pending))
        {
            return false;
        }
        _pending.Remove(context);
        if (pending.Count == 0)
        {
            return false;
        }
        logs = pending.Select(p => p.ToAuditLog()).ToList();
        return true;
    }

    private static List<PendingAudit> StampAndCollect(DbContext context, CurrentUserInfo user)
    {
        context.ChangeTracker.DetectChanges();
        var now = DateTime.UtcNow;
        var pending = new List<PendingAudit>();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            // Die Protokoll-Tabellen selbst werden nicht auditiert.
            if (entry.Entity is AuditLog or AccessLog)
            {
                continue;
            }

            var originalState = entry.State;

            if (entry.Entity is IAuditable auditable)
            {
                if (originalState == EntityState.Added)
                {
                    auditable.CreatedAt = now;
                    auditable.CreatedById = user.Id;
                }
                else if (originalState == EntityState.Modified)
                {
                    auditable.ModifiedAt = now;
                    auditable.ModifiedById = user.Id;
                }
            }

            var isRestoration = false;
            if (originalState == EntityState.Deleted && entry.Entity is ISoftDelete soft)
            {
                // Hard-Delete → Soft-Delete umwandeln.
                entry.State = EntityState.Modified;
                soft.IsDeleted = true;
                soft.DeletedAt = now;
                soft.DeletedById = user.Id;
            }
            else if (originalState == EntityState.Modified && entry.Entity is ISoftDelete
                     && entry.Property(nameof(ISoftDelete.IsDeleted)) is { IsModified: true } flag
                     && flag.OriginalValue is true && flag.CurrentValue is false)
            {
                isRestoration = true;
            }

            if (entry.Entity is not IAuditable)
            {
                continue;
            }

            var action = originalState switch
            {
                EntityState.Added => AuditAction.Created,
                EntityState.Deleted => AuditAction.Deleted,
                EntityState.Modified when entry.Entity is ISoftDelete s && s.IsDeleted => AuditAction.Deleted,
                EntityState.Modified when isRestoration => AuditAction.Restored,
                EntityState.Modified => AuditAction.Modified,
                _ => (AuditAction?)null,
            };

            if (action is not null)
            {
                pending.Add(new PendingAudit(entry, action.Value, now, user));
            }
        }

        return pending;
    }

    /// <summary>
    /// Hält einen Eintrag fest, dessen finaler Primärschlüssel erst nach dem Speichern feststeht.
    /// Geänderte Felder werden bereits beim Sammeln (alt → neu) erfasst.
    /// </summary>
    private sealed class PendingAudit
    {
        private readonly EntityEntry _entry;
        private readonly AuditAction _action;
        private readonly DateTime _timestamp;
        private readonly CurrentUserInfo _user;
        private readonly Dictionary<string, object?[]> _changes = new();

        public PendingAudit(EntityEntry entry, AuditAction action, DateTime timestamp, CurrentUserInfo user)
        {
            _entry = entry;
            _action = action;
            _timestamp = timestamp;
            _user = user;

            if (action is AuditAction.Modified or AuditAction.Deleted or AuditAction.Restored)
            {
                foreach (var prop in entry.Properties)
                {
                    if (prop.IsModified && !Equals(prop.OriginalValue, prop.CurrentValue))
                    {
                        _changes[prop.Metadata.Name] = new[] { prop.OriginalValue, prop.CurrentValue };
                    }
                }
            }
        }

        public AuditLog ToAuditLog() => new()
        {
            Timestamp = _timestamp,
            AgentId = _user.Id,
            AgentName = _user.Name,
            EntityType = _entry.Entity.GetType().Name,
            EntityId = KeyString(_entry),
            Action = _action,
            ChangesJson = _changes.Count > 0 ? JsonSerializer.Serialize(_changes) : null,
        };

        private static string KeyString(EntityEntry entry)
        {
            var key = entry.Metadata.FindPrimaryKey();
            if (key is null)
            {
                return string.Empty;
            }

            var values = key.Properties.Select(p => entry.Property(p.Name).CurrentValue?.ToString() ?? "");
            return string.Join(",", values);
        }
    }
}
