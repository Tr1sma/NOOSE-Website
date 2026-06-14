namespace NOOSE_Website.Infrastructure.Shares;

/// <summary>
/// Prozessweiter (Singleton) In-Memory-Broadcaster für die Live-Aktualisierung des „Freigaben“-Badges in der
/// NavMenu. Der Freigabe-Posteingang meldet nach jeder Entscheidung (Registrierung/Namensänderung/Taskforce-/
/// Beförderungs-/Hochstufungs-Antrag), dass sich die Anzahl offener Vorgänge geändert hat; offene NavMenu-
/// Komponenten (je Blazor-Server-Circuit) abonnieren und zählen neu. Single-VPS-Betrieb → ein In-Memory-
/// Broadcaster genügt, kein SignalR-Hub nötig. (Analog <see cref="Notifications.NotificationBroadcaster"/>.)
/// </summary>
public sealed class SharesBroadcaster
{
    /// <summary>Wird ausgelöst, sobald sich die Anzahl offener Posteingangs-Vorgänge geändert haben könnte.</summary>
    public event Action? Modified;

    public void Report() => Modified?.Invoke();
}
