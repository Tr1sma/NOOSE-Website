using NOOSE_Website.Services.Statistics;

namespace NOOSE_Website.Infrastructure.Statistics;

/// <summary>
/// Erzeugt den automatischen Monats-Lagebericht (Phase 8 / Block D, Schritt 2). Tickt täglich und prüft, ob für
/// den zuletzt abgeschlossenen Monat (Vormonat) bereits ein Bericht existiert; falls nicht, wird er erzeugt und
/// archiviert. Die Existenz des Berichts ist zugleich der Merker – kein separater Zustand nötig, idempotent.
/// Beim ersten Start nach dem Deploy entsteht so sofort der Bericht des Vormonats. Best-effort: ein Fehler wird
/// nur geloggt, der Dienst läuft weiter. Eigener DI-Scope je Durchlauf (Singleton-HostedService darf keine
/// Scoped-Dienste injizieren).
/// </summary>
public sealed class SituationReportWorker(IServiceScopeFactory scopeFactory, ILogger<SituationReportWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Kurze Anlaufverzögerung (DB-Verbindung/Migration sicher abgeschlossen), dann sofort einmal prüfen.
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
