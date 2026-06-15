namespace NOOSE_Website.Infrastructure.Announcements;

/// <summary>
/// Prozessweiter (Singleton) In-Memory-Broadcaster für die Live-Aktualisierung des „Schwarzes Brett“-Badges
/// (offene Quittierungen) in der NavMenu. Der <c>AnkuendigungService</c> meldet nach einer Quittierung die
/// betroffene Agent-Id; offene NavMenu-Komponenten (je Blazor-Server-Circuit) abonnieren und zählen neu, wenn
/// die gemeldete Id der eigenen entspricht. Single-VPS-Betrieb → ein In-Memory-Broadcaster genügt, kein SignalR-
/// Hub nötig. (Empfängerspezifisch wie <see cref="Notifications.NotificationBroadcaster"/>, da der Badge pro
/// Agent gilt – so rechnet nicht jeder Online-Agent bei jeder fremden Quittierung neu.)
/// </summary>
public sealed class AcknowledgmentBroadcaster
{
    /// <summary>Wird mit der betroffenen Agent-Id ausgelöst, sobald sich dessen Anzahl offener Quittierungen ändert.</summary>
    public event Action<string>? Modified;

    public void Report(string agentId) => Modified?.Invoke(agentId);
}
