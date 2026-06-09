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
            var user = currentUserService.GetAsync().GetAwaiter().GetResult();
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
        WritePendingAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    private async Task WritePendingAsync(DbContext? context, CancellationToken cancellationToken)
    {
        if (context is null || !_pending.TryGetValue(context, out var pending))
        {
            return;
        }

        // Pro Context nur einmal verarbeiten – auch der re-entrante AuditLog-Save unten landet hier
        // (dann mit leerer Liste, da AuditLog nicht auditierbar ist) und wird so sauber abgeräumt.
        _pending.Remove(context);
        if (pending.Count == 0)
        {
            return;
        }

        var logs = pending.Select(p => p.ToAuditLog()).ToList();
        context.Set<AuditLog>().AddRange(logs);
        // Erneutes Speichern; löst keine Rekursion aus, da AuditLog weder IAuditable noch ISoftDelete ist.
        await context.SaveChangesAsync(cancellationToken);
    }

    private static List<PendingAudit> StampAndCollect(DbContext context, CurrentUserInfo user)
    {
        context.ChangeTracker.DetectChanges();
        var now = DateTime.UtcNow;
        var pending = new List<PendingAudit>();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            // Die Protokoll-Tabellen selbst werden nicht auditiert.
            if (entry.Entity is AuditLog or ZugriffsLog)
            {
                continue;
            }

            var originalState = entry.State;

            if (entry.Entity is IAuditable auditable)
            {
                if (originalState == EntityState.Added)
                {
                    auditable.ErstelltAm = now;
                    auditable.ErstelltVonId = user.Id;
                }
                else if (originalState == EntityState.Modified)
                {
                    auditable.GeaendertAm = now;
                    auditable.GeaendertVonId = user.Id;
                }
            }

            var istWiederherstellung = false;
            if (originalState == EntityState.Deleted && entry.Entity is ISoftDelete soft)
            {
                // Hard-Delete → Soft-Delete umwandeln.
                entry.State = EntityState.Modified;
                soft.IstGeloescht = true;
                soft.GeloeschtAm = now;
                soft.GeloeschtVonId = user.Id;
            }
            else if (originalState == EntityState.Modified && entry.Entity is ISoftDelete
                     && entry.Property(nameof(ISoftDelete.IstGeloescht)) is { IsModified: true } flag
                     && flag.OriginalValue is true && flag.CurrentValue is false)
            {
                istWiederherstellung = true;
            }

            if (entry.Entity is not IAuditable)
            {
                continue;
            }

            var aktion = originalState switch
            {
                EntityState.Added => AuditAktion.Erstellt,
                EntityState.Deleted => AuditAktion.Geloescht,
                EntityState.Modified when entry.Entity is ISoftDelete s && s.IstGeloescht => AuditAktion.Geloescht,
                EntityState.Modified when istWiederherstellung => AuditAktion.Wiederhergestellt,
                EntityState.Modified => AuditAktion.Geaendert,
                _ => (AuditAktion?)null,
            };

            if (aktion is not null)
            {
                pending.Add(new PendingAudit(entry, aktion.Value, now, user));
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
        private readonly AuditAktion _aktion;
        private readonly DateTime _zeitpunkt;
        private readonly CurrentUserInfo _user;
        private readonly Dictionary<string, object?[]> _aenderungen = new();

        public PendingAudit(EntityEntry entry, AuditAktion aktion, DateTime zeitpunkt, CurrentUserInfo user)
        {
            _entry = entry;
            _aktion = aktion;
            _zeitpunkt = zeitpunkt;
            _user = user;

            if (aktion is AuditAktion.Geaendert or AuditAktion.Geloescht or AuditAktion.Wiederhergestellt)
            {
                foreach (var prop in entry.Properties)
                {
                    if (prop.IsModified && !Equals(prop.OriginalValue, prop.CurrentValue))
                    {
                        _aenderungen[prop.Metadata.Name] = new[] { prop.OriginalValue, prop.CurrentValue };
                    }
                }
            }
        }

        public AuditLog ToAuditLog() => new()
        {
            Zeitpunkt = _zeitpunkt,
            AgentId = _user.Id,
            AgentName = _user.Name,
            EntitaetTyp = _entry.Entity.GetType().Name,
            EntitaetId = KeyString(_entry),
            Aktion = _aktion,
            AenderungenJson = _aenderungen.Count > 0 ? JsonSerializer.Serialize(_aenderungen) : null,
        };

        private static string KeyString(EntityEntry entry)
        {
            var key = entry.Metadata.FindPrimaryKey();
            if (key is null)
            {
                return string.Empty;
            }

            var werte = key.Properties.Select(p => entry.Property(p.Name).CurrentValue?.ToString() ?? "");
            return string.Join(",", werte);
        }
    }
}
