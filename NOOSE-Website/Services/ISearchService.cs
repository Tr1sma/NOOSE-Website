using NOOSE_Website.Models.Querschnitt;

namespace NOOSE_Website.Services;

/// <summary>
/// Globale Suche über alle durchsuchbaren Akten-Inhalte (Person, Dok, Quelle, Kommentar, Fraktion,
/// Personengruppe, Partei, Operation). LIKE-basiert (findet Teilwörter; identisch auf MariaDB-dev und
/// MySQL-prod). Verschlusssachen und Papierkorb werden über die jeweilige Eltern-Akte gefiltert.
/// Optional: <see cref="SuchKriterien.Fuzzy"/> ergänzt Tippfehler-Toleranz (Levenshtein, in-memory),
/// <see cref="SuchKriterien.MaxModus"/> weitet die Suche auf alle Nebenfelder + Inhalte aus.
/// </summary>
public interface ISearchService
{
    Task<List<SuchErgebnisGruppe>> SuchenAsync(SuchKriterien kriterien, bool istFuehrung, CancellationToken cancellationToken = default);

    /// <summary>Schnelle Personensuche für die Command-Palette/Topbar (mit immer leicht aktiver Tippfehler-Toleranz).</summary>
    Task<List<SchnellTreffer>> SchnellsucheAsync(string text, bool istFuehrung, int max = 8, CancellationToken cancellationToken = default);
}
