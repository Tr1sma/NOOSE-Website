using NOOSE_Website.Models.Abstractions;

namespace NOOSE_Website.Data.Entities.Fraktionen;

/// <summary>
/// Eine Aktivität/Aktion einer Fraktion (z. B. „Überfall Fleeca Bank", Art „Raub") für den Aktivitäts-Zeitstrahl.
/// Trägt einen vom Nutzer gesetzten <see cref="Zeitpunkt"/> (wann die Aktion stattfand – Datum + Uhrzeit) getrennt
/// von den Audit-Zeitstempeln (<see cref="IAuditable"/>, = wann der Eintrag erfasst/geändert wurde). Voll auditiert
/// und papierkorbfähig (<see cref="ISoftDelete"/>). Eine eigene Verschlusssache-Stufe gibt es nicht – die Aktivität
/// erbt die Sichtbarkeit ihrer Eltern-Fraktion (Gate im Service sowie zentral in <c>Sichtbarkeit</c>, das auch die
/// an die Aktivität gehängten Quellen/Anhänge schützt). Verknüpfte Dokumente laufen über die generische
/// Quellen-Engine (<c>Quelle</c> mit <c>EntitaetTyp = nameof(FraktionAktivitaet)</c>).
/// </summary>
public class FraktionAktivitaet : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string FraktionId { get; set; } = string.Empty;
    public Fraktion? Fraktion { get; set; }

    /// <summary>Kurzer Titel der Aktivität (Pflicht), z. B. „Überfall Fleeca Bank".</summary>
    public string Titel { get; set; } = string.Empty;

    /// <summary>Art/Kategorie als Freitext mit Vorschlägen (z. B. „Raub", „Geiselnahme"); optional.</summary>
    public string? Art { get; set; }

    /// <summary>Zeitpunkt der Aktion (Datum + Uhrzeit, als UTC gespeichert) – bestimmt die Sortierung im Zeitstrahl.</summary>
    public DateTime Zeitpunkt { get; set; }

    /// <summary>Freitext-Beschreibung der Aktivität; optional.</summary>
    public string? Beschreibung { get; set; }

    /// <summary>Ort der Aktion (Freitext); optional.</summary>
    public string? Ort { get; set; }

    // ---- IAuditable (Erfassungs-/Änderungszeitpunkt des Eintrags) ----
    public DateTime ErstelltAm { get; set; }
    public string? ErstelltVonId { get; set; }
    public DateTime? GeaendertAm { get; set; }
    public string? GeaendertVonId { get; set; }

    // ---- ISoftDelete ----
    public bool IstGeloescht { get; set; }
    public DateTime? GeloeschtAm { get; set; }
    public string? GeloeschtVonId { get; set; }
}
