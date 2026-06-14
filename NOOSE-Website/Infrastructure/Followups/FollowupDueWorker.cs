using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;

namespace NOOSE_Website.Infrastructure.Followups;

/// <summary>
/// Wiederkehrender Hintergrund-Dienst, der fällige, noch nicht gemeldete Wiedervorlagen erkennt und je Eintrag eine
/// „Wiedervorlage fällig"-Benachrichtigung an den Zuständigen sowie die Follower der Akte verschickt – aus deren
/// Sicht Verschlusssache-geprüft (kein Leck an Nicht-Berechtigte). Der <see cref="Wiedervorlage.BenachrichtigtAm"/>-
/// Stempel verhindert Doppel-Meldungen. Best-effort: ein Fehler in einem Durchlauf wird nur geloggt, der Dienst
/// läuft weiter. Eigener DI-Scope je Durchlauf (Singleton-HostedService darf keine Scoped-Dienste injizieren).
/// </summary>
public sealed class FollowupDueWorker(IServiceScopeFactory scopeFactory, ILogger<FollowupDueWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Kurze Anlaufverzögerung, damit Start/DB-Verbindung sicher stehen.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await ProcessDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Wiedervorlage-Fälligkeitsprüfung fehlgeschlagen.");
            }
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task ProcessDueAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var watchlist = scope.ServiceProvider.GetRequiredService<IWatchlistService>();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;

        // Offen + fällig + noch nicht gemeldet (Soft-Delete-Filter blendet Gelöschte automatisch aus).
        var due = await db.Followups
            .Where(w => !w.Done && w.DueAt <= now && w.NotifiedAt == null)
            .OrderBy(w => w.DueAt)
            .ToListAsync(cancellationToken);
        if (due.Count == 0)
        {
            return;
        }

        // Akten-Namen (öffentlich, nie Klarname) + VS-Flag + Href in einer Sammelabfrage.
        var refs = due.Select(w => (w.EntityType, w.EntityId)).Distinct().ToList();
        var resolved = await RecordsReference.ResolveAsync(db, refs, cancellationToken);

        foreach (var w in due)
        {
            // Akte im Papierkorb/unbekannt → nicht melden, aber stempeln (kein Dauer-Reprocessing je Durchlauf).
            if (!resolved.TryGetValue((w.EntityType, w.EntityId), out var record))
            {
                w.NotifiedAt = now;
                continue;
            }

            // Empfänger = Zuständiger + aktive Follower der Akte.
            var recipientIds = new HashSet<string>(StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(w.ResponsibleAgentId))
            {
                recipientIds.Add(w.ResponsibleAgentId);
            }
            foreach (var followerId in await watchlist.GetFollowerIdsAsync(w.EntityType, w.EntityId, cancellationToken))
            {
                recipientIds.Add(followerId);
            }

            if (recipientIds.Count > 0)
            {
                // Nur aktive Empfänger, und je Empfänger geprüft: Verschlusssache nur an die Führung,
                // Taskforces nur an Zugeteilte (Mitgliedschaft) – über IstAkteSichtbarAsync mit der Empfänger-Id.
                var active = await db.Users
                    .Where(u => recipientIds.Contains(u.Id) && u.Status == AgentStatus.Active)
                    .Select(u => new { u.Id, u.IsAdmin, u.Rank })
                    .ToListAsync(cancellationToken);
                var allowed = new List<string>();
                foreach (var u in active)
                {
                    var uLeadership = u.IsAdmin || u.Rank is >= Rank.SupervisorySpecialAgent;
                    if (await Visibility.IsRecordVisibleAsync(db, w.EntityType, w.EntityId, uLeadership, cancellationToken, u.Id))
                    {
                        allowed.Add(u.Id);
                    }
                }

                if (allowed.Count > 0)
                {
                    var title = BuildTitle(record.Display, w.Note);
                    await notifications.NotifyManyAsync(allowed, NotificationType.Followup, title, record.Href,
                        triggerId: null, cancellationToken);
                }
            }

            w.NotifiedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static string BuildTitle(string display, string? note)
    {
        var title = $"Wiedervorlage fällig: „{display}“";
        if (!string.IsNullOrWhiteSpace(note))
        {
            title += $" – {note.Trim()}";
        }
        // Benachrichtigung.Titel ist auf 300 Zeichen begrenzt.
        return title.Length > 300 ? title[..297] + "…" : title;
    }
}
