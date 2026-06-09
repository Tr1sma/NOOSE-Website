using NOOSE_Website.Models.Querschnitt;

namespace NOOSE_Website.Services;

/// <summary>
/// Globale Suche über alle durchsuchbaren Akten-Inhalte (Person, Dok, Quelle, Kommentar, Fraktion,
/// Personengruppe). LIKE-basiert (findet Teilwörter; identisch auf MariaDB-dev und MySQL-prod).
/// Verschlusssachen und Papierkorb werden über die jeweilige Eltern-Akte gefiltert.
/// </summary>
public interface ISearchService
{
    Task<List<SuchErgebnisGruppe>> SuchenAsync(SuchKriterien kriterien, bool istFuehrung, CancellationToken cancellationToken = default);

    /// <summary>Schnelle Personensuche für die Command-Palette/Topbar.</summary>
    Task<List<SchnellTreffer>> SchnellsucheAsync(string text, bool istFuehrung, int max = 8, CancellationToken cancellationToken = default);
}
