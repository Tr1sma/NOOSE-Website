using NOOSE_Website.Services.Statistik;

namespace NOOSE_Website.Infrastructure.Statistik;

/// <summary>
/// Erzeugt den automatischen Monats-Lagebericht (Phase 8 / Block D, Schritt 2). Tickt täglich und prüft, ob für
/// den zuletzt abgeschlossenen Monat (Vormonat) bereits ein Bericht existiert; falls nicht, wird er erzeugt und
/// archiviert. Die Existenz des Berichts ist zugleich der Merker – kein separater Zustand nötig, idempotent.
/// Beim ersten Start nach dem Deploy entsteht so sofort der Bericht des Vormonats. Best-effort: ein Fehler wird
/// nur geloggt, der Dienst läuft weiter. Eigener DI-Scope je Durchlauf (Singleton-HostedService darf keine
/// Scoped-Dienste injizieren).
/// </summary>
public sealed class LageberichtDienst(IServiceScopeFactory scopeFactory, ILogger<LageberichtDienst> logger)
    : BackgroundService
{
    private static readonly TimeSpan Intervall = TimeSpan.FromHours(24);

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

        using var timer = new PeriodicTimer(Intervall);
        do
        {
            try
            {
                await PruefeAsync(stoppingToken);
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
        while (await SicherWartenAsync(timer, stoppingToken));
    }

    private static async Task<bool> SicherWartenAsync(PeriodicTimer timer, CancellationToken stoppingToken)
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

    private async Task PruefeAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ILageberichtService>();
        if (await service.ErzeugeFaelligenAsync(DateTime.UtcNow, cancellationToken))
        {
            logger.LogInformation("Automatischer Monats-Lagebericht für den abgeschlossenen Vormonat erzeugt.");
        }
    }
}
