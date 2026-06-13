using NOOSE_Website.Models.Statistik;

namespace NOOSE_Website.Services.Statistik;

/// <summary>
/// Liefert die aggregierten Auswertungen der Statistik-Seite (Phase 8 / Block D): Verteilungen über
/// Einstufung, Gefährdung, Lebensstatus, Maßnahme-Ausgänge und Vorgangs-Status, die Top-Listen der
/// gefährlichsten Personen/Fraktionen sowie eine 12-Monats-Zeitreihe. Alle Abfragen respektieren den
/// Verschlusssachen-Filter des aufrufenden Agents (über <paramref name="istFuehrung"/>).
/// </summary>
public interface IStatistikService
{
    // meId = Agent-Id des Betrachters; nötig, weil die wiederverwendeten Dashboard-Kennzahlen die
    // Taskforce-Mitgliedschafts-Sichtbarkeit berücksichtigen. Übrige Auswertungen sind VS-gefiltert via istFuehrung.
    /// <summary>
    /// Erzeugt den vollständigen Statistik-Report aus Sicht des Aufrufers. <paramref name="topN"/> begrenzt
    /// die Top-Listen der gefährlichsten Personen/Fraktionen.
    /// </summary>
    Task<StatistikReport> GetReportAsync(bool istFuehrung, string? meId, int topN = 10, CancellationToken cancellationToken = default);
}
