using NOOSE_Website.Services.Public;

namespace NOOSE_Website.Infrastructure.Public;

/// <summary>Flips public wanted notices past their expiry date to Abgelaufen and tells leadership once per sweep.</summary>
/// <remarks>
/// Not a security control. The read path already hides an expired notice by date, so a dead, delayed or (with several
/// host instances) doubled worker leaks nothing — it only leaves the internal status dishonest. Which is exactly why
/// nobody may drop the expiry filter from the read path "because the worker handles it now".
/// Holds no DbContext on purpose: the belt, the status rules and the cache invalidation live in the service.
/// </remarks>
public sealed class PublicWantedExpiryWorker(IServiceScopeFactory scopeFactory, ILogger<PublicWantedExpiryWorker> logger)
    : BackgroundService
{
    // the expiry date is the end of a chosen day, so "that same evening" is the accuracy that matters; a five-minute
    // tick would be 288 pointless queries a day
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // longer than the other workers: migrations and the four seeders run first
            await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Public wanted expiry sweep failed.");
            }
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPublicWantedService>();
        var expired = await service.ExpireDueAsync(cancellationToken);
        if (expired > 0)
        {
            logger.LogInformation("Public wanted expiry: {Anzahl} notices taken offline.", expired);
        }
    }
}
