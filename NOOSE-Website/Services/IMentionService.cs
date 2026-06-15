using NOOSE_Website.Models.Common;

namespace NOOSE_Website.Services;

/// <summary>
/// Auflösung und Suche von @-Verlinkungen (Mentions). Wandelt gespeicherten Text mit <c>@{Typ:Id}</c>-Tokens in
/// anzeigefertige Segmente (Text + aufgelöste, klickbare Verweise – Verschlusssachen für Nicht-Führung verborgen)
/// und liefert die Kandidaten für den @-Picker beim Verfassen. Wiederverwendbar (Chat heute, später Kommentare/Doks).
/// </summary>
public interface IMentionService
{
    // meId = Agent-Id des Betrachters; fremde Taskforce-Erwähnungen erscheinen als „(nicht verfügbar)".
    /// <summary>Zerlegt den Text in Segmente und löst die Mention-Tokens (Name/Route/Verschlusssache) auf.</summary>
    Task<IReadOnlyList<MentionSegment>> ResolveAsync(string? text, bool isLeadership, string? meId, CancellationToken cancellationToken = default);

    /// <summary>Löst mehrere Texte in EINER Sammelabfrage auf (z. B. eine ganze Chat-Liste) – Reihenfolge bleibt erhalten.</summary>
    Task<IReadOnlyList<IReadOnlyList<MentionSegment>>> ResolveManyAsync(IReadOnlyList<string?> texts, bool isLeadership, string? meId, CancellationToken cancellationToken = default);

    /// <summary>Suchvorschläge für den @-Picker: Akten (alle Typen) + Quellen + Agenten. <paramref name="darfVerschlusssacheLesen"/>
    /// steuert die VS-Sicht (Führung ODER Nur-Lese-Aufsicht), <paramref name="darfKlarname"/> getrennt die Klarname-Sicht
    /// (nur echte Führung, NICHT die Aufsicht). Taskforces nur, wenn der Aufrufer zugeteilt ist (oder alle sehen darf) – daher <paramref name="meId"/>.</summary>
    Task<List<MentionHit>> CandidatesAsync(string? text, bool mayClassifiedRead, bool mayRealName, string? meId, CancellationToken cancellationToken = default);
}
