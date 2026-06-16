using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Infrastructure.Chat;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="ITaskforceChatService" />
public class TaskforceChatService(IDbContextFactory<AppDbContext> dbFactory, TaskforceChatBroadcaster broadcaster,
    INotificationService notifications) : ITaskforceChatService
{
    public async Task<List<TaskforceMessage>> GetMessagesAsync(string taskforceId, bool isLeadership, int limit = 100, DateTime? olderAs = null, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await Visibility.IsRecordVisibleAsync(db, nameof(Taskforce), taskforceId, isLeadership, cancellationToken))
        {
            return new();
        }

        var query = db.TaskforceMessages.Where(n => n.TaskforceId == taskforceId);
        if (olderAs is not null)
        {
            query = query.Where(n => n.CreatedAt < olderAs.Value);
        }
        // newest first, reverse
        var latest = await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken);
        latest.Reverse();
        return latest;
    }

    public async Task<TaskforceMessage> SendAsync(string taskforceId, string text, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        var content = text?.Trim();
        if (string.IsNullOrEmpty(content))
        {
            throw new InvalidOperationException("Die Nachricht darf nicht leer sein.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // visibility check
        if (!await Visibility.IsRecordVisibleAsync(db, nameof(Taskforce), taskforceId, actor.IsLeadership(), cancellationToken))
        {
            throw new UnauthorizedAccessException("Diese Taskforce ist für dich nicht zugänglich.");
        }

        var message = new TaskforceMessage
        {
            TaskforceId = taskforceId,
            Text = content,
            AuthorName = actor.GetCodename(),
        };
        db.TaskforceMessages.Add(message);
        await db.SaveChangesAsync(cancellationToken);

        broadcaster.Report(taskforceId);

        // notify mentions
        try
        {
            var who = string.IsNullOrWhiteSpace(actor.GetCodename()) ? "Ein Agent" : actor.GetCodename();
            await notifications.NotifyMentionedAsync(content, $"{who} hat dich im Taskforce-Chat erwähnt.",
                $"/taskforces/{taskforceId}", nameof(Taskforce), taskforceId, actor, cancellationToken);
        }
        catch { /* best effort */ }

        return message;
    }

    public async Task DeleteAsync(string messageId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var message = await db.TaskforceMessages.FirstOrDefaultAsync(n => n.Id == messageId, cancellationToken);
        if (message is null)
        {
            return;
        }
        // author or leadership
        if (!actor.IsLeadership() && message.CreatedById != actor.GetAgentId())
        {
            throw new UnauthorizedAccessException("Nur der Autor oder die Führung kann eine Nachricht zurückziehen.");
        }
        db.TaskforceMessages.Remove(message); // soft delete
        await db.SaveChangesAsync(cancellationToken);

        broadcaster.Report(message.TaskforceId);
    }
}
