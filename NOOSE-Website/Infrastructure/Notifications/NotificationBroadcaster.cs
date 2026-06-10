namespace NOOSE_Website.Infrastructure.Notifications;

/// <summary>
/// Prozessweiter (Singleton) In-Memory-Broadcaster für die Live-Aktualisierung der Glocke. Der
/// <c>NotificationService</c> meldet nach dem Anlegen/Lesen einer Benachrichtigung die betroffene
/// <c>EmpfaengerId</c>; offene Glocken-Komponenten (je Blazor-Server-Circuit) abonnieren und laden neu, wenn die
/// gemeldete Id der eigenen entspricht. Single-VPS-Betrieb (siehe Plan.md Phase 10) → ein In-Memory-Broadcaster
/// genügt, kein SignalR-Hub nötig. (Analog <see cref="Chat.TaskforceChatBroadcaster"/>.)
/// </summary>
public sealed class NotificationBroadcaster
{
    /// <summary>Wird mit der betroffenen Empfänger-Agent-Id ausgelöst, sobald sich dessen Benachrichtigungen ändern.</summary>
    public event Action<string>? Empfangen;

    public void Melde(string empfaengerId) => Empfangen?.Invoke(empfaengerId);
}
