namespace NOOSE_Website.Infrastructure.Chat;

/// <summary>
/// Prozessweiter (Singleton) In-Memory-Broadcaster für die Live-Aktualisierung der Taskforce-Chats. Der
/// Chat-Service meldet nach Senden/Löschen die betroffene <c>TaskforceId</c>; offene Chat-Panels (je Blazor-Server-
/// Circuit) abonnieren das Ereignis und laden ihren Verlauf neu. Single-VPS-Betrieb (siehe Plan.md Phase 10) →
/// ein In-Memory-Broadcaster genügt, kein SignalR-Hub nötig.
/// </summary>
public sealed class TaskforceChatBroadcaster
{
    /// <summary>Wird mit der betroffenen TaskforceId ausgelöst, sobald sich deren Chat ändert.</summary>
    public event Action<string>? Geaendert;

    public void Melde(string taskforceId) => Geaendert?.Invoke(taskforceId);
}
