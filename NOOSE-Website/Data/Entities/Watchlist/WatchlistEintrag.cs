using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Watchlist;

/// <summary>
/// Ein „Folgen"-Eintrag: ein Agent (<see cref="AgentId"/>) beobachtet eine Akte (polymorph über
/// <see cref="EntitaetTyp"/> + <see cref="EntitaetId"/>, wie Kommentar/Quelle/Tag). Ändert sich die
/// gefolgte Akte, erhält der Agent eine „Beobachtete Akte geändert"-Benachrichtigung (Glocke).
/// Voll auditiert und papierkorbfähig. Bewusst KEIN Unique-Index (Projekt-Konvention „soft-deletebar →
/// kein Unique-Index"); Entfolgen ist ein Soft-Delete, erneutes Folgen reaktiviert die alte Zeile –
/// die Aktiv-Eindeutigkeit prüft der <c>WatchlistService</c> per Aktiv-Abfrage (analog FraktionMitglied).
/// </summary>
[Table("Watchlisten")]
public class WatchlistEintrag : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Agent-Id des Folgers (Identity-User-Id).</summary>
    public string AgentId { get; set; } = string.Empty;

    [Column("EntitaetTyp")]
    public string EntitaetTyp { get; set; } = string.Empty;
    [Column("EntitaetId")]
    public string EntitaetId { get; set; } = string.Empty;

    // ---- IAuditable ----
    [Column("ErstelltAm")]
    public DateTime ErstelltAm { get; set; }
    [Column("ErstelltVonId")]
    public string? ErstelltVonId { get; set; }
    [Column("GeaendertAm")]
    public DateTime? GeaendertAm { get; set; }
    [Column("GeaendertVonId")]
    public string? GeaendertVonId { get; set; }

    // ---- ISoftDelete ----
    [Column("IstGeloescht")]
    public bool IstGeloescht { get; set; }
    [Column("GeloeschtAm")]
    public DateTime? GeloeschtAm { get; set; }
    [Column("GeloeschtVonId")]
    public string? GeloeschtVonId { get; set; }
}
