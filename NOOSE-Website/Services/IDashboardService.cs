using NOOSE_Website.Models.Dashboard;

namespace NOOSE_Website.Services;

/// <summary>
/// Liefert die Daten des Lagezentrums (Startseite): die vier Kennzahl-Kacheln und den
/// Aktivitäts-Feed der zuletzt vorgenommenen Änderungen. Alle Abfragen respektieren den
/// Verschlusssachen-Filter des aufrufenden Agents.
/// </summary>
public interface IDashboardService
{
    // meId = Agent-Id des Betrachters; nötig für die Taskforce-Mitgliedschafts-Sichtbarkeit (Nicht-Führung sieht
    // nur zugeteilte Taskforces in den Kennzahlen/Listen). Übrige Akten-Typen weiterhin VS-gefiltert via istFuehrung.
    /// <summary>Kennzahlen für die vier Kacheln (Personen, Fraktionen &amp; Personengruppen, offene Anträge, Verschlusssachen).</summary>
    Task<DashboardKennzahlen> GetKennzahlenAsync(bool istFuehrung, string? meId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Akten mit Aktualisierungsbedarf (Ampel gelb oder rot, älteste zuerst) – damit man sieht, was mal wieder
    /// aktualisiert werden sollte. VS-gefiltert; auf <paramref name="max"/> Einträge begrenzt.
    /// </summary>
    Task<List<DashboardVeralteteAkte>> GetAktualisierungsbedarfAsync(bool istFuehrung, string? meId, int max = 30, CancellationToken cancellationToken = default);

    /// <summary>
    /// Die Fraktionen als echte Liste, nach Gefährdungsstufe absteigend sortiert (gefährlichste zuerst). VS-gefiltert.
    /// </summary>
    Task<List<DashboardFraktionGefaehrdung>> GetFraktionenNachGefaehrdungAsync(bool istFuehrung, CancellationToken cancellationToken = default);

    /// <summary>
    /// Die jüngsten Änderungen über alle Akten hinweg (neueste zuerst), aufgelöst auf die jeweilige
    /// Eltern-Akte inkl. Anzeigename. Kind-Änderungen (Doks, Mitglieder, Agent-Zuteilungen) werden auf
    /// ihre Akte hochgerollt; Verschlusssachen erscheinen nur für die Führung.
    /// </summary>
    Task<List<DashboardAenderung>> GetLetzteAenderungenAsync(bool istFuehrung, int max = 8, CancellationToken cancellationToken = default);

    /// <summary>
    /// Die vier Verteilungs-Diagramme (§248): Fälle nach Einstufung, Maßnahme-Ausgänge, Fraktionen nach
    /// Gefährdung und offene Anträge nach Art. Alle Zählungen sind VS-gefiltert (für Nicht-Führung nur
    /// nicht-klassifizierte Akten), damit kein Verschlusssachen-Bestand durchsickert.
    /// </summary>
    Task<DashboardVerteilungen> GetVerteilungenAsync(bool istFuehrung, string? meId, CancellationToken cancellationToken = default);
}
