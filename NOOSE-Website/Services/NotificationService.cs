using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Notifications;
using NOOSE_Website.Infrastructure.Notifications;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="INotificationService" />
public class NotificationService(IDbContextFactory<AppDbContext> dbFactory, NotificationBroadcaster broadcaster)
    : INotificationService
{
    public async Task NotifyAsync(string? recipientId, NotificationType type, string title, string? href,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recipientId))
        {
            return;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.Notifications.Add(new Notification
        {
            RecipientId = recipientId,
            Type = type,
            Title = title,
            Href = href,
        });
        await db.SaveChangesAsync(cancellationToken);

        broadcaster.Report(recipientId);
    }

    public async Task NotifyMentionedAsync(string? text, string title, string? href, string targetType, string targetId,
        ClaimsPrincipal trigger, CancellationToken cancellationToken = default)
    {
        // exclude self, dedupe
        var triggerId = trigger.GetAgentId();
        var agentIds = MentionParser.Parse(text)
            .Where(t => t.Type == nameof(Agent) && t.Id != triggerId)
            .Select(t => t.Id)
            .Distinct()
            .ToList();
        if (agentIds.Count == 0)
        {
            return;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // active recipients only
        var recipient = await db.Users
            .Where(u => agentIds.Contains(u.Id) && u.Status == AgentStatus.Active)
            .Select(u => new { u.Id, u.IsAdmin, u.Rank })
            .ToListAsync(cancellationToken);

        var notified = new List<string>();
        foreach (var e in recipient)
        {
            // gate on recipient's own visibility, not the trigger's (no record/classification leak)
            var recipientIsLeadership = e.IsAdmin || e.Rank is >= Rank.SupervisorySpecialAgent;
            if (!await Visibility.IsRecordVisibleAsync(db, targetType, targetId, recipientIsLeadership, cancellationToken))
            {
                continue;
            }
            db.Notifications.Add(new Notification
            {
                RecipientId = e.Id,
                Type = NotificationType.Mention,
                Title = title,
                Href = href,
            });
            notified.Add(e.Id);
        }

        if (notified.Count == 0)
        {
            return;
        }
        await db.SaveChangesAsync(cancellationToken);

        foreach (var id in notified)
        {
            broadcaster.Report(id);
        }
    }

    public async Task NotifyManyAsync(IReadOnlyCollection<string> recipientIds, NotificationType type,
        string title, string? href, string? triggerId, CancellationToken cancellationToken = default)
    {
        // exclude trigger, dedupe, drop empties
        var targets = recipientIds
            .Where(id => !string.IsNullOrWhiteSpace(id) && id != triggerId)
            .Distinct()
            .ToList();
        if (targets.Count == 0)
        {
            return;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        foreach (var id in targets)
        {
            db.Notifications.Add(new Notification
            {
                RecipientId = id,
                Type = type,
                Title = title,
                Href = href,
            });
        }
        await db.SaveChangesAsync(cancellationToken);

        foreach (var id in targets)
        {
            broadcaster.Report(id);
        }
    }

    public async Task<List<Notification>> GetOwnAsync(ClaimsPrincipal actor, int max = 20, CancellationToken cancellationToken = default)
    {
        // recipient is always the caller — derive from principal, never a parameter
        var agentId = actor.GetAgentId();
        if (string.IsNullOrWhiteSpace(agentId))
        {
            return new();
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Notifications
            .Where(n => n.RecipientId == agentId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(Math.Clamp(max, 1, 100))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetUnreadCountAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        var agentId = actor.GetAgentId();
        if (string.IsNullOrWhiteSpace(agentId))
        {
            return 0;
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Notifications
            .CountAsync(n => n.RecipientId == agentId && n.ReadAt == null, cancellationToken);
    }

    public async Task AsReadMarkAsync(string notificationId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        var agentId = actor.GetAgentId();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var n = await db.Notifications.FirstOrDefaultAsync(x => x.Id == notificationId, cancellationToken);
        // only own notification may be marked read
        if (n is null || n.RecipientId != agentId || n.ReadAt is not null)
        {
            return;
        }
        n.ReadAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        broadcaster.Report(n.RecipientId);
    }

    public async Task AllAsReadAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        var agentId = actor.GetAgentId();
        if (string.IsNullOrWhiteSpace(agentId))
        {
            return;
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var open = await db.Notifications
            .Where(n => n.RecipientId == agentId && n.ReadAt == null)
            .ToListAsync(cancellationToken);
        if (open.Count == 0)
        {
            return;
        }
        var now = DateTime.UtcNow;
        foreach (var n in open)
        {
            n.ReadAt = now;
        }
        await db.SaveChangesAsync(cancellationToken);

        broadcaster.Report(agentId);
    }
}
