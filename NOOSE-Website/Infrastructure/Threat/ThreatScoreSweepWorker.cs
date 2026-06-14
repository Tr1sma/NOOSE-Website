using NOOSE_Website.Services;

namespace NOOSE_Website.Infrastructure.Threat;

/// <summary>
/// Täglicher Hintergrund-Sweep, der die Bedrohungs-Scores aller Fraktionen neu berechnet. Nötig, weil der
/// Score zeit-abklingende Komponenten (S1-Heat, Halbwertszeit 90 Tage) enthält: ohne Schreibevent „kühlt" eine
/// Fraktion ab, ohne dass der persistierte Wert das mitbekäme. Der ereignisgetriebene Recompute hält den Score
/// bei jeder inhaltlichen Änderung aktuell; dieser Sweep fängt die reine Zeit-Drift ab und seedet beim ersten
/// Start alle noch unberechneten Fraktionen. Best-effort: ein Fehler wird nur geloggt, der Dienst läuft weiter.
/// Eigener DI-Scope je Durchlauf (Singleton-HostedService darf keine Scoped-Dienste injizieren).
/// </summary>
public sealed class ThreatScoreSweepWorker(IServiceScopeFactory scopeFactory, ILogger<ThreatScoreSweepWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Kurze Anlaufverzögerung (DB-Verbindung/Migration sicher abgeschlossen), dann sofort einmal seeden.
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
                logger.LogError(ex, "Bedrohungs-Score-Sweep fehlgeschlagen.");
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
        logger.LogInformation("Bedrohungs-Score-Sweep: {Fraktionen} Fraktionen + {Personen} Personen neu berechnet.",
            factions, people);
    }
}
