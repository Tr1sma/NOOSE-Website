using NOOSE_Website.Models.Abstractions;

namespace NOOSE_Website.Data.Entities.Watchlist;

/// <summary>
/// Ein „Folgen"-Eintrag: ein Agent (<see cref="AgentId"/>) beobachtet eine Akte (polymorph über
/// <see cref="EntitaetTyp"/> + <see cref="EntitaetId"/>, wie Kommentar/Quelle/Tag). Ändert sich die
/// gefolgte Akte, erhält der Agent eine „Beobachtete Akte geändert"-Benachrichtigung (Glocke).
/// Voll auditiert und papierkorbfähig. Bewusst KEIN Unique-Index (Projekt-Konvention „soft-deletebar →
/// kein Unique-Index"); Entfolgen ist ein Soft-Delete, erneutes Folgen reaktiviert die alte Zeile –
/// die Aktiv-Eindeutigkeit prüft der <c>WatchlistService</c> per Aktiv-Abfrage (analog FraktionMitglied).
/// </summary>
public class WatchlistEintrag : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Agent-Id des Folgers (Identity-User-Id).</summary>
    public string AgentId { get; set; } = string.Empty;

    public string EntitaetTyp { get; set; } = string.Empty;
    public string EntitaetId { get; set; } = string.Empty;

    // ---- IAuditable ----
    public DateTime ErstelltAm { get; set; }
    public string? ErstelltVonId { get; set; }
    public DateTime? GeaendertAm { get; set; }
    public string? GeaendertVonId { get; set; }

    // ---- ISoftDelete ----
    public bool IstGeloescht { get; set; }
    public DateTime? GeloeschtAm { get; set; }
    public string? GeloeschtVonId { get; set; }
}
