using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;

namespace NOOSE_Website.Infrastructure.Wiedervorlagen;

/// <summary>
/// Wiederkehrender Hintergrund-Dienst, der fällige, noch nicht gemeldete Wiedervorlagen erkennt und je Eintrag eine
/// „Wiedervorlage fällig"-Benachrichtigung an den Zuständigen sowie die Follower der Akte verschickt – aus deren
/// Sicht Verschlusssache-geprüft (kein Leck an Nicht-Berechtigte). Der <see cref="Wiedervorlage.BenachrichtigtAm"/>-
/// Stempel verhindert Doppel-Meldungen. Best-effort: ein Fehler in einem Durchlauf wird nur geloggt, der Dienst
/// läuft weiter. Eigener DI-Scope je Durchlauf (Singleton-HostedService darf keine Scoped-Dienste injizieren).
/// </summary>
public sealed class WiedervorlageFaelligkeitsDienst(IServiceScopeFactory scopeFactory, ILogger<WiedervorlageFaelligkeitsDienst> logger)
    : BackgroundService
{
    private static readonly TimeSpan Intervall = TimeSpan.FromMinutes(5);

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

        using var timer = new PeriodicTimer(Intervall);
        do
        {
            try
            {
                await VerarbeiteFaelligeAsync(stoppingToken);
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
        while (await SicherWartenAsync(timer, stoppingToken));
    }

    private static async Task<bool> SicherWartenAsync(PeriodicTimer timer, CancellationToken stoppingToken)
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

    private async Task VerarbeiteFaelligeAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var watchlist = scope.ServiceProvider.GetRequiredService<IWatchlistService>();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var jetzt = DateTime.UtcNow;

        // Offen + fällig + noch nicht gemeldet (Soft-Delete-Filter blendet Gelöschte automatisch aus).
        var faellige = await db.Wiedervorlagen
            .Where(w => !w.Erledigt && w.FaelligAm <= jetzt && w.BenachrichtigtAm == null)
            .OrderBy(w => w.FaelligAm)
            .ToListAsync(cancellationToken);
        if (faellige.Count == 0)
        {
            return;
        }

        // Akten-Namen (öffentlich, nie Klarname) + VS-Flag + Href in einer Sammelabfrage.
        var refs = faellige.Select(w => (w.EntitaetTyp, w.EntitaetId)).Distinct().ToList();
        var aufgeloest = await AktenReferenz.AufloesenAsync(db, refs, cancellationToken);

        foreach (var w in faellige)
        {
            // Akte im Papierkorb/unbekannt → nicht melden, aber stempeln (kein Dauer-Reprocessing je Durchlauf).
            if (!aufgeloest.TryGetValue((w.EntitaetTyp, w.EntitaetId), out var akte))
            {
                w.BenachrichtigtAm = jetzt;
                continue;
            }

            // Empfänger = Zuständiger + aktive Follower der Akte.
            var empfaengerIds = new HashSet<string>(StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(w.ZustaendigerAgentId))
            {
                empfaengerIds.Add(w.ZustaendigerAgentId);
            }
            foreach (var followerId in await watchlist.GetFollowerIdsAsync(w.EntitaetTyp, w.EntitaetId, cancellationToken))
            {
                empfaengerIds.Add(followerId);
            }

            if (empfaengerIds.Count > 0)
            {
                // Nur aktive Empfänger, und je Empfänger geprüft: Verschlusssache nur an die Führung,
                // Taskforces nur an Zugeteilte (Mitgliedschaft) – über IstAkteSichtbarAsync mit der Empfänger-Id.
                var aktive = await db.Users
                    .Where(u => empfaengerIds.Contains(u.Id) && u.Status == AgentStatus.Aktiv)
                    .Select(u => new { u.Id, u.IstAdmin, u.Dienstgrad })
                    .ToListAsync(cancellationToken);
                var erlaubt = new List<string>();
                foreach (var u in aktive)
                {
                    var uFuehrung = u.IstAdmin || u.Dienstgrad is >= Dienstgrad.SupervisorySpecialAgent;
                    if (await Sichtbarkeit.IstAkteSichtbarAsync(db, w.EntitaetTyp, w.EntitaetId, uFuehrung, cancellationToken, u.Id))
                    {
                        erlaubt.Add(u.Id);
                    }
                }

                if (erlaubt.Count > 0)
                {
                    var titel = BaueTitel(akte.Anzeige, w.Notiz);
                    await notifications.BenachrichtigeVieleAsync(erlaubt, NotificationTyp.Wiedervorlage, titel, akte.Href,
                        ausloeserId: null, cancellationToken);
                }
            }

            w.BenachrichtigtAm = jetzt;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static string BaueTitel(string anzeige, string? notiz)
    {
        var titel = $"Wiedervorlage fällig: „{anzeige}“";
        if (!string.IsNullOrWhiteSpace(notiz))
        {
            titel += $" – {notiz.Trim()}";
        }
        // Benachrichtigung.Titel ist auf 300 Zeichen begrenzt.
        return titel.Length > 300 ? titel[..297] + "…" : titel;
    }
}
