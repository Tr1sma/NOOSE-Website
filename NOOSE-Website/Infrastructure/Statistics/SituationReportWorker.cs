using NOOSE_Website.Services.Statistics;

namespace NOOSE_Website.Infrastructure.Statistics;

/// <summary>Daily worker that generates the previous month's situation report if it does not yet exist (idempotent).</summary>
public sealed class SituationReportWorker(IServiceScopeFactory scopeFactory, ILogger<SituationReportWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(90), stoppingToken);
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
                await CheckAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lagebericht-Prüfung fehlgeschlagen.");
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

    private async Task CheckAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ISituationReportService>();
        if (await service.GenerateDueAsync(DateTime.UtcNow, cancellationToken))
        {
            logger.LogInformation("Automatischer Monats-Lagebericht für den abgeschlossenen Vormonat erzeugt.");
        }
    }
}
