using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Services;

namespace NOOSE_Website.Infrastructure.Jobs;

/// <summary>Background worker that reminds assignees of open jobs approaching their due date (3 days, 1 day, due day).</summary>
public sealed class JobDueSoonWorker(IServiceScopeFactory scopeFactory, ILogger<JobDueSoonWorker> logger)
    : BackgroundService
{
    // due dates are day-granular, so a 15-minute cadence is plenty
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

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
                logger.LogError(ex, "Aufgaben-Fälligkeitsprüfung fehlgeschlagen.");
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
        // local day, matching how due dates are stored (date picker) and how the UI flags overdue
        var today = DateTime.Today;

        // only open jobs due within the outermost (3-day) window with a milestone still pending
        var candidates = await db.Jobs
            .Where(j => j.Status == JobStatus.Open
                     && j.DueDate != null
                     && j.DueDate < today.AddDays(4)
                     && j.DueReminderStage < JobDueReminderStage.DueDay)
            .ToListAsync(cancellationToken);
        if (candidates.Count == 0)
        {
            return;
        }

        foreach (var job in candidates)
        {
            var daysUntil = (job.DueDate!.Value.Date - today).Days;
            var desired =
                daysUntil <= 0 ? JobDueReminderStage.DueDay :
                daysUntil == 1 ? JobDueReminderStage.OneDay :
                daysUntil <= 3 ? JobDueReminderStage.ThreeDays :
                JobDueReminderStage.None;

            if (desired <= job.DueReminderStage)
            {
                continue;
            }

            // overdue-without-prior-ping advances silently; on-time milestones notify assignees
            if (daysUntil >= 0)
            {
                var assigneeIds = await db.JobAssignments
                    .Where(z => z.JobId == job.Id)
                    .Select(z => z.AgentId)
                    .ToListAsync(cancellationToken);
                if (assigneeIds.Count > 0)
                {
                    var active = await db.Users
                        .Where(u => assigneeIds.Contains(u.Id) && u.Status == AgentStatus.Active)
                        .Select(u => u.Id)
                        .ToListAsync(cancellationToken);
                    if (active.Count > 0)
                    {
                        var when = daysUntil == 0 ? "heute" : daysUntil == 1 ? "morgen" : $"in {daysUntil} Tagen";
                        await notifications.NotifyManyAsync(active, NotificationType.JobDueSoon,
                            $"Aufgabe {when} fällig: „{job.Title}“.", $"/aufgaben/{job.Id}",
                            triggerId: null, cancellationToken);
                    }
                }
            }

            job.DueReminderStage = desired;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
