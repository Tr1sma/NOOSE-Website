namespace NOOSE_Website.Infrastructure.Notifications;

/// <summary>Process-wide in-memory broadcaster for live notification-bell updates.</summary>
public sealed class NotificationBroadcaster
{
    /// <summary>Fired with the affected recipient agent id when their notifications change.</summary>
    public event Action<string>? Received;

    public void Report(string recipientId) => Received?.Invoke(recipientId);
}
