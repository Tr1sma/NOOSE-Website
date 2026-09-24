using NOOSE_Website.Data.Entities.Notifications;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Notifications;

/// <summary>One page of the caller's own notifications, filtered.</summary>
/// <param name="Types">Only these types; empty = every type.</param>
/// <param name="FromUtc">Created at or after this instant.</param>
/// <param name="ToUtc">Created before this instant (exclusive).</param>
/// <param name="OnlyUnread">Only notices still counting toward the badge.</param>
/// <param name="Page">1-based; clamped to the last page.</param>
/// <param name="PageSize">Rows per page; clamped to 10..100.</param>
public sealed record NotificationInboxQuery(
    IReadOnlyCollection<NotificationType> Types,
    DateTime? FromUtc,
    DateTime? ToUtc,
    bool OnlyUnread,
    int Page,
    int PageSize = NotificationInboxQuery.DefaultPageSize)
{
    public const int DefaultPageSize = 25;
}

/// <summary>A page of the inbox plus what the pager needs.</summary>
public sealed record NotificationInboxPage(List<Notification> Items, int Total, int Page, int PageCount)
{
    // fresh list per call, never shared
    public static NotificationInboxPage Empty => new([], 0, 1, 1);
}

/// <summary>The inbox filter as the page holds it: local calendar days, not instants.</summary>
/// <param name="Types">Selected types; empty = every type.</param>
/// <param name="From">First local day shown, inclusive.</param>
/// <param name="To">Last local day shown, inclusive.</param>
/// <param name="OnlyUnread">Only unread notices.</param>
public sealed record NotificationInboxFilter(
    IReadOnlyList<NotificationType> Types, DateTime? From, DateTime? To, bool OnlyUnread)
{
    public static NotificationInboxFilter None => new([], null, null, false);
}

/// <summary>A notification type the caller has received, with how many.</summary>
public sealed record NotificationTypeCount(NotificationType Type, int Count);
