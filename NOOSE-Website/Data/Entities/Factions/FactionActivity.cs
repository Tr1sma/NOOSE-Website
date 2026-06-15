using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Factions;

/// <summary>
/// Eine Aktivität/Aktion einer Fraktion (z. B. „Überfall Fleeca Bank", Art „Raub") für den Aktivitäts-Zeitstrahl.
/// Trägt einen vom Nutzer gesetzten <see cref="Zeitpunkt"/> (wann die Aktion stattfand – Datum + Uhrzeit) getrennt
/// von den Audit-Zeitstempeln (<see cref="IAuditable"/>, = wann der Eintrag erfasst/geändert wurde). Voll auditiert
/// und papierkorbfähig (<see cref="ISoftDelete"/>). Eine eigene Verschlusssache-Stufe gibt es nicht – die Aktivität
/// erbt die Sichtbarkeit ihrer Eltern-Fraktion (Gate im Service sowie zentral in <c>Sichtbarkeit</c>, das auch die
/// an die Aktivität gehängten Quellen/Anhänge schützt). Verknüpfte Dokumente laufen über die generische
/// Quellen-Engine (<c>Quelle</c> mit <c>EntitaetTyp = nameof(FraktionAktivitaet)</c>).
/// </summary>
[Table("FraktionAktivitaeten")]
public class FactionActivity : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("FraktionId")]
    public string FactionId { get; set; } = string.Empty;
    public Faction? Faction { get; set; }

    /// <summary>Kurzer Titel der Aktivität (Pflicht), z. B. „Überfall Fleeca Bank".</summary>
    [Column("Titel")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Art/Kategorie als Freitext mit Vorschlägen (z. B. „Raub", „Geiselnahme"); optional.</summary>
    [Column("Art")]
    public string? Kind { get; set; }

    /// <summary>Zeitpunkt der Aktion (Datum + Uhrzeit, als UTC gespeichert) – bestimmt die Sortierung im Zeitstrahl.</summary>
    [Column("Zeitpunkt")]
    public DateTime Timestamp { get; set; }

    /// <summary>Freitext-Beschreibung der Aktivität; optional.</summary>
    [Column("Beschreibung")]
    public string? Description { get; set; }

    /// <summary>Ort der Aktion (Freitext); optional.</summary>
    [Column("Ort")]
    public string? Location { get; set; }

    // ---- IAuditable (Erfassungs-/Änderungszeitpunkt des Eintrags) ----
    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }

    // ---- ISoftDelete ----
    [Column("IstGeloescht")]
    public bool IsDeleted { get; set; }
    [Column("GeloeschtAm")]
    public DateTime? DeletedAt { get; set; }
    [Column("GeloeschtVonId")]
    public string? DeletedById { get; set; }
}
