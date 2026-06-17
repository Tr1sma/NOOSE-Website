using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace NOOSE_Website.Infrastructure.Notifications;

/// <summary>Decouples watchlist fan-out from the save: dispatches fire-and-forget in a fresh scope so notify errors can't roll back the committed action.</summary>
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
