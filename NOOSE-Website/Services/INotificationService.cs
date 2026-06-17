using System.Security.Claims;
using NOOSE_Website.Data.Entities.Notifications;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>In-app notifications (bell). Best-effort: created in a separate context after the action so a failed notification never rolls back the core action.</summary>
public interface INotificationService
{
    /// <summary>Create a notification for one recipient (no-op on empty recipient id).</summary>
    Task NotifyAsync(string? recipientId, NotificationType type, string title, string? href,
        CancellationToken cancellationToken = default);

    /// <summary>Notify agents mentioned via @{Agent:Id} in the text, excluding the trigger, deduplicated and visibility-filtered. Title stays generic.</summary>
    Task NotifyMentionedAsync(string? text, string title, string? href, string targetType, string targetId,
        ClaimsPrincipal trigger, CancellationToken cancellationToken = default);

    /// <summary>Broadcast the same notification to many recipients, excluding the trigger and deduplicated; empty list = no-op.</summary>
    Task NotifyManyAsync(IReadOnlyCollection<string> recipientIds, NotificationType type, string title,
        string? href, string? triggerId, CancellationToken cancellationToken = default);

    /// <summary>Caller's latest notifications, newest first.</summary>
    Task<List<Notification>> GetOwnAsync(ClaimsPrincipal actor, int max = 20, CancellationToken cancellationToken = default);

    /// <summary>Caller's unread notification count (badge).</summary>
    Task<int> GetUnreadCountAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Mark one of the caller's notifications as read.</summary>
    Task AsReadMarkAsync(string notificationId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Mark all of the caller's unread notifications as read.</summary>
    Task AllAsReadAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
