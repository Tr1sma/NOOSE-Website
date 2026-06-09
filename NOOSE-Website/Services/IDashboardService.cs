using NOOSE_Website.Models.Dashboard;

namespace NOOSE_Website.Services;

/// <summary>
/// Liefert die Daten des Lagezentrums (Startseite): die vier Kennzahl-Kacheln und den
/// Aktivitäts-Feed der zuletzt vorgenommenen Änderungen. Alle Abfragen respektieren den
/// Verschlusssachen-Filter des aufrufenden Agents.
/// </summary>
public interface IDashboardService
{
    /// <summary>Kennzahlen für die vier Kacheln (Personen, Fraktionen &amp; Personengruppen, offene Anträge, Verschlusssachen).</summary>
    Task<DashboardKennzahlen> GetKennzahlenAsync(bool istFuehrung, CancellationToken cancellationToken = default);

    /// <summary>
    /// Die jüngsten Änderungen über alle Akten hinweg (neueste zuerst), aufgelöst auf die jeweilige
    /// Eltern-Akte inkl. Anzeigename. Kind-Änderungen (Doks, Mitglieder, Agent-Zuteilungen) werden auf
    /// ihre Akte hochgerollt; Verschlusssachen erscheinen nur für die Führung.
    /// </summary>
    Task<List<DashboardAenderung>> GetLetzteAenderungenAsync(bool istFuehrung, int max = 8, CancellationToken cancellationToken = default);
}
