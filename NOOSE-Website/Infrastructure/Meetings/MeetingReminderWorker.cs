using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;

namespace NOOSE_Website.Infrastructure.Meetings;

/// <summary>Background worker that reminds the meeting roster shortly before a planned meeting starts.</summary>
public sealed class MeetingReminderWorker(IServiceScopeFactory scopeFactory, ILogger<MeetingReminderWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private const int DayLeadHours = 24;
    private const int SoonLeadMinutes = 30;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
                logger.LogError(ex, "Meeting reminder run failed.");
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

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var soonThreshold = now.AddMinutes(SoonLeadMinutes);
        var dayThreshold = now.AddHours(DayLeadHours);
        var sent = 0;

        // 30-minute reminder; claims the day stamp too so a last-minute meeting is not reminded twice
        var soonDue = await db.Meetings
            .Where(m => m.Status == MeetingStatus.Planned && m.ReminderSoonSentAt == null
                     && m.Start > now && m.Start <= soonThreshold)
            .OrderBy(m => m.Start)
            .Select(m => new { m.Id, m.Title, m.Start })
            .ToListAsync(cancellationToken);
        foreach (var m in soonDue)
        {
            // claim first, and only the version we read: a concurrent reschedule must lose the claim
            var claimed = await db.Meetings
                .Where(x => x.Id == m.Id && x.ReminderSoonSentAt == null && x.Start == m.Start)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.ReminderSoonSentAt, now)
                    .SetProperty(x => x.ReminderDaySentAt, now), cancellationToken);
            if (claimed == 0)
            {
                continue;
            }
            if (await NotifyAsync(db, notifications, m.Id, m.Start,
                    $"Besprechung „{m.Title}“ beginnt um {MeetingTime.Text(m.Start)} Uhr – in Kürze.", cancellationToken))
            {
                sent++;
            }
        }

        // one-day reminder; excludes the last 30 minutes so it never races the soon reminder
        var dayDue = await db.Meetings
            .Where(m => m.Status == MeetingStatus.Planned && m.ReminderDaySentAt == null
                     && m.Start > soonThreshold && m.Start <= dayThreshold)
            .OrderBy(m => m.Start)
            .Select(m => new { m.Id, m.Title, m.Start })
            .ToListAsync(cancellationToken);
        foreach (var m in dayDue)
        {
            var claimed = await db.Meetings
                .Where(x => x.Id == m.Id && x.ReminderDaySentAt == null && x.Start == m.Start)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ReminderDaySentAt, now), cancellationToken);
            if (claimed == 0)
            {
                continue;
            }
            if (await NotifyAsync(db, notifications, m.Id, m.Start,
                    $"Erinnerung: Besprechung „{m.Title}“ am {MeetingTime.Text(m.Start)} Uhr.", cancellationToken))
            {
                sent++;
            }
        }

        if (sent > 0)
        {
            logger.LogInformation("Meeting reminders: {Anzahl} sent.", sent);
        }
    }

    /// <summary>Notifies the roster minus anyone excused; returns true if a notification was sent.</summary>
    private async Task<bool> NotifyAsync(AppDbContext db, INotificationService notifications,
        string meetingId, DateTime meetingStart, string title, CancellationToken cancellationToken)
    {
        // time-zone conversion is not EF-translatable
        var day = MeetingTime.Day(meetingStart);

        var roster = await db.Users.AsNoTracking().OnlySelectable()
            .Where(u => (u.ReleasedAt ?? u.RegisteredAt) <= meetingStart)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
        if (roster.Count == 0)
        {
            return false;
        }

        var excused = new HashSet<string>(StringComparer.Ordinal);
        foreach (var agentId in await db.MeetingSignOffs.AsNoTracking()
            .Where(s => s.MeetingId == meetingId)
            .Select(s => s.AgentId)
            .ToListAsync(cancellationToken))
        {
            excused.Add(agentId);
        }
        foreach (var agentId in await db.Absences.AsNoTracking().Covering(day)
            .Select(a => a.AgentId)
            .ToListAsync(cancellationToken))
        {
            excused.Add(agentId);
        }

        var recipients = roster.Where(id => !excused.Contains(id)).ToList();
        if (recipients.Count == 0)
        {
            return false;
        }

        try
        {
            await notifications.NotifyManyAsync(recipients, NotificationType.MeetingReminder,
                title.Length > 300 ? title[..297] + "…" : title,
                $"/besprechungen/{meetingId}", triggerId: null, cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // the claim is already consumed; log this meeting and keep the batch going
            logger.LogError(ex, "Meeting reminder for {Aktenzeichen} failed after claiming.", meetingId);
            return false;
        }
    }
}
