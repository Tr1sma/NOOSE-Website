using System.Security.Claims;
using NOOSE_Website.Data.Entities.Notifications;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>
/// In-App-Benachrichtigungen (Glocke). Erzeugt Benachrichtigungen an diskreten Ereignissen (Antrag entschieden,
/// @-Erwähnung, Konto-Ereignisse) und liest sie für die Glocke. Best-effort: das Anlegen geschieht in einem eigenen
/// Factory-Context nach der eigentlichen Aktion – eine fehlgeschlagene Benachrichtigung darf die Kernaktion nie
/// zurückrollen. Live-Aktualisierung über den <c>NotificationBroadcaster</c>.
/// </summary>
public interface INotificationService
{
    /// <summary>Legt eine Benachrichtigung für genau einen Empfänger an (no-op bei leerer Empfänger-Id).</summary>
    Task NotifyAsync(string? recipientId, NotificationType type, string title, string? href,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Benachrichtigt alle in <paramref name="text"/> per <c>@{Agent:Id}</c> erwähnten Agenten – ohne den Auslöser
    /// selbst, dedupliziert und Verschlusssache-gefiltert (Empfänger ohne Sicht auf die Ziel-Akte werden NICHT
    /// benachrichtigt). Der Titel bleibt bewusst generisch (kein Akten-/Verschlusssachen-Name).
    /// </summary>
    Task NotifyMentionedAsync(string? text, string title, string? href, string targetType, string targetId,
        ClaimsPrincipal trigger, CancellationToken cancellationToken = default);

    /// <summary>
    /// Legt dieselbe Benachrichtigung für viele Empfänger an (Broadcast/Rundnachricht). Der Auslöser
    /// (<paramref name="ausloeserId"/>) wird ausgeschlossen, Empfänger-Ids werden dedupliziert; leere Liste = no-op.
    /// Ein Batch-Insert in einem Context, danach je Empfänger ein Live-Signal.
    /// </summary>
    Task NotifyManyAsync(IReadOnlyCollection<string> recipientIds, NotificationType type, string title,
        string? href, string? triggerId, CancellationToken cancellationToken = default);

    /// <summary>Die eigenen neuesten Benachrichtigungen des Aufrufers (für die Glocke), neueste zuerst.</summary>
    Task<List<Notification>> GetOwnAsync(ClaimsPrincipal actor, int max = 20, CancellationToken cancellationToken = default);

    /// <summary>Anzahl ungelesener Benachrichtigungen des Aufrufers (Badge).</summary>
    Task<int> GetUnreadCountAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Markiert eine Benachrichtigung als gelesen (nur die eigene des Aufrufers).</summary>
    Task AsReadMarkAsync(string notificationId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Markiert alle ungelesenen Benachrichtigungen des Aufrufers als gelesen.</summary>
    Task AllAsReadAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
