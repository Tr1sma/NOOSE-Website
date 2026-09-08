using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;
using NOOSE_Website.Services.Public;

namespace NOOSE_Website.Infrastructure.Tickets;

/// <summary>Reminds a citizen once when their ticket waits on them, and closes it when they never answer.</summary>
/// <remarks>
/// Runs per host instance, like every other worker here — no multi-instance operation against one database.
/// The clock is the server's, so <c>TZ=Europe/Berlin</c> matters for when the day boundary falls, though both
/// spans here are counted in whole days from a UTC stamp and never from a local midnight.
/// <para>
/// The automatic closure is a system write and names no agent: <c>ClosedById</c> stays null rather than
/// borrowing whoever happened to handle the ticket, and the reason is the one the desk would have picked.
/// </para>
/// </remarks>
public sealed class TicketFollowupWorker(IServiceScopeFactory scopeFactory, ILogger<TicketFollowupWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    /// <summary>Rows touched per pass; a backlog is worked off over the following hours instead of in one go.</summary>
    private const int BatchSize = 200;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
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
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Automatische Ticket-Nachfassung fehlgeschlagen.");
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

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await SweepAsync(db, notifications, DateTime.UtcNow, cancellationToken);
    }

    /// <summary>One pass over the tickets waiting on a citizen.</summary>
    /// <remarks>Public and static with the clock as a parameter, because a BackgroundService cannot otherwise
    /// be exercised: the timing is the whole behaviour here, and the test project sees no internals.</remarks>
    public static async Task SweepAsync(AppDbContext db, INotificationService notifications, DateTime now,
        CancellationToken cancellationToken)
    {
        var nudgeBefore = now - TicketRules.NudgeAfter;
        var closeBefore = now - TicketRules.AutoCloseAfter;

        // one query for both jobs: the older half closes, the rest is reminded once
        var waiting = await db.Tickets
            .Where(t => t.Status == TicketStatus.WartetAufBuerger)
            .Where(t => t.LastActivityAt <= nudgeBefore)
            .OrderBy(t => t.LastActivityAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);
        if (waiting.Count == 0)
        {
            return;
        }

        // one lookup for the whole batch rather than one per row
        var profileIds = waiting.Select(t => t.CitizenProfileId).Where(x => x != null).Distinct().ToList();
        var accounts = profileIds.Count == 0
            ? new Dictionary<string, string?>()
            : (await db.BuergerProfile.AsNoTracking()
                .Where(p => profileIds.Contains(p.Id))
                .Select(p => new { p.Id, p.UserId })
                .ToListAsync(cancellationToken))
                .ToDictionary(p => p.Id, p => p.UserId, StringComparer.Ordinal);

        var rung = new List<(string UserId, string CaseNumber, bool Closed)>();
        foreach (var row in waiting)
        {
            var userId = row.CitizenProfileId is { } profileId
                ? accounts.GetValueOrDefault(profileId)
                : null;

            // the rule table stays the authority even here, where the query already narrowed to one status:
            // this write bypasses SetStatusAsync, so it must not be the place a later edge change goes unnoticed
            if (row.LastActivityAt <= closeBefore
                && TicketRules.IsTransitionAllowed(row.Status, TicketStatus.Geschlossen))
            {
                row.Status = TicketStatus.Geschlossen;
                row.ClosedAt = now;
                // a system closure names no agent
                row.ClosedById = null;
                row.ClosingReason = TicketAbschlussgrund.KeinKontakt;
                row.ClosingNote = null;
                row.NudgedAt = null;
                row.LastActivityAt = now;
                if (userId is not null)
                {
                    rung.Add((userId, row.CaseNumber, true));
                }
                continue;
            }
            if (row.NudgedAt is not null)
            {
                continue;
            }
            // the stamp, not the activity: reminding must not look like movement on the ticket
            row.NudgedAt = now;
            if (userId is not null)
            {
                rung.Add((userId, row.CaseNumber, false));
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        foreach (var (userId, caseNumber, closed) in rung)
        {
            try
            {
                // the same folding notifier and the same category the desk rings on; no new one
                await notifications.NotifyOnceAsync(userId, NotificationType.PublicTicketAnswered,
                    closed
                        ? $"Ticket {caseNumber} wurde ohne Rückmeldung abgeschlossen"
                        : $"Ticket {caseNumber} wartet auf deine Antwort",
                    $"/buerger/tickets/{caseNumber}", cancellationToken);
            }
            catch
            {
                /* best effort */
            }
        }
    }
}
