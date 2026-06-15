using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace NOOSE_Website.Infrastructure.Notifications;

/// <summary>
/// Entkoppelt den Watchlist-Benachrichtigungs-Fan-out vom Speichervorgang: der <c>WatchlistAenderungInterceptor</c>
/// übergibt nach erfolgreichem Commit die betroffenen Akten; der eigentliche Versand läuft <b>fire-and-forget</b> in
/// einem frischen DI-Scope. So blockiert das Speichern nie auf der Folger-Ermittlung, und ein Fehler beim
/// Benachrichtigen kann die bereits committete Kernaktion niemals zurückrollen (best-effort, wie der übrige
/// Benachrichtigungs-Pfad). Singleton – hält selbst keinen DbContext/scoped Dienst, sondern öffnet je Versand einen
/// eigenen Scope.
/// </summary>
public sealed class WatchlistDispatcher(IServiceScopeFactory scopeFactory, ILogger<WatchlistDispatcher> logger)
{
    public void Distribute(string? actorId, IReadOnlyCollection<(string Type, string Id)> records)
    {
        if (records.Count == 0)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var fanout = scope.ServiceProvider.GetRequiredService<WatchlistFanout>();
                await fanout.ProcessAsync(actorId, records, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Watchlist-Benachrichtigung fehlgeschlagen.");
            }
        });
    }
}
