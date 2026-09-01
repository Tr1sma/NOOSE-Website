using NOOSE_Website.Services;

namespace NOOSE_Website.Infrastructure.Gamification;

/// <summary>Daily worker that posts the top-agent announcement once the configured interval elapses.</summary>
public sealed class TopAgentAwardWorker(IServiceScopeFactory scopeFactory, ILogger<TopAgentAwardWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(120), stoppingToken);
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
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<ITopAgentAwardService>();
                var result = await service.RunDueAsync(DateTime.UtcNow, stoppingToken);
                if (result.Posted)
                {
                    logger.LogInformation("Top-agent announcement posted ({Placed} placed).", result.Ranked);
                }
                else if (result.Ranked > 0 && !result.Announced)
                {
                    // a stale LastRun means this retries tomorrow; silence would hide a dead webhook
                    logger.LogWarning("Top-agent announcement not delivered ({Placed} placed).", result.Ranked);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Top-agent announcement failed.");
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
}
