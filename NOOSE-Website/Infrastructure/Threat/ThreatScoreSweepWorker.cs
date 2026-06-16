using NOOSE_Website.Services;

namespace NOOSE_Website.Infrastructure.Threat;

/// <summary>Daily background sweep; recalculates threat scores.</summary>
public sealed class ThreatScoreSweepWorker(IServiceScopeFactory scopeFactory, ILogger<ThreatScoreSweepWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // startup delay
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
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
                logger.LogError(ex, "Threat score sweep failed.");
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
        var service = scope.ServiceProvider.GetRequiredService<IThreatScoreService>();
        var factions = await service.NewCalculateAllAsync(cancellationToken);
        var people = await service.NewCalculateAllPeopleScoresAsync(cancellationToken);
        logger.LogInformation("Threat sweep: {Fraktionen} factions + {Personen} people recalculated.",
            factions, people);
    }
}
