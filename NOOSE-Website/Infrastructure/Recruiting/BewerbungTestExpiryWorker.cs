using NOOSE_Website.Services;

namespace NOOSE_Website.Infrastructure.Recruiting;

/// <summary>Hands in aptitude-test attempts whose processing time ran out while the browser was closed.</summary>
/// <remarks>
/// Not a security control. In the normal case the applicant's own countdown submits, and the read, draft and submit
/// paths each re-check the deadline themselves — so a dead, delayed or (with several host instances) doubled worker
/// grants no extra time; it only leaves the attempt open longer than it should be. Which is exactly why nobody may
/// drop the deadline check from those paths "because the worker handles it now".
/// Holds no DbContext on purpose: the deadline rule, the claim and the notifications live in the service.
/// </remarks>
public sealed class BewerbungTestExpiryWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<BewerbungTestExpiryWorker> logger) : BackgroundService
{
    // the limit is configured in minutes, so a quarter-hourly sweep would leave a 20-minute test showing
    // "läuft" for a quarter of its own length after it died
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // after the migrations and the seeders, and clear of the other workers' start times
            await Task.Delay(TimeSpan.FromSeconds(75), stoppingToken);
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
                logger.LogError(ex, "Recruiting test expiry sweep failed.");
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
        var service = scope.ServiceProvider.GetRequiredService<IBewerbungTestExpiryService>();
        var expired = await service.ExpireDueAsync(cancellationToken);
        if (expired > 0)
        {
            logger.LogInformation("Recruiting test expiry: {Anzahl} attempts handed in automatically.", expired);
        }
    }
}
