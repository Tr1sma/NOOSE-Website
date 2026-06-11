using NOOSE_Website.Models.Querschnitt;

namespace NOOSE_Website.Services;

/// <summary>
/// Auflösung und Suche von @-Verlinkungen (Mentions). Wandelt gespeicherten Text mit <c>@{Typ:Id}</c>-Tokens in
/// anzeigefertige Segmente (Text + aufgelöste, klickbare Verweise – Verschlusssachen für Nicht-Führung verborgen)
/// und liefert die Kandidaten für den @-Picker beim Verfassen. Wiederverwendbar (Chat heute, später Kommentare/Doks).
/// </summary>
public interface IMentionService
{
    /// <summary>Zerlegt den Text in Segmente und löst die Mention-Tokens (Name/Route/Verschlusssache) auf.</summary>
    Task<IReadOnlyList<MentionSegment>> AufloesenAsync(string? text, bool istFuehrung, CancellationToken cancellationToken = default);

    /// <summary>Löst mehrere Texte in EINER Sammelabfrage auf (z. B. eine ganze Chat-Liste) – Reihenfolge bleibt erhalten.</summary>
    Task<IReadOnlyList<IReadOnlyList<MentionSegment>>> AufloesenVieleAsync(IReadOnlyList<string?> texte, bool istFuehrung, CancellationToken cancellationToken = default);

    /// <summary>Suchvorschläge für den @-Picker: Akten (alle Typen) + Quellen + Agenten. Verschlusssache-gefiltert;
    /// Taskforces nur, wenn der Aufrufer zugeteilt ist (oder alle sehen darf) – daher <paramref name="meId"/>.</summary>
    Task<List<MentionTreffer>> KandidatenAsync(string? text, bool istFuehrung, string? meId, CancellationToken cancellationToken = default);
}
