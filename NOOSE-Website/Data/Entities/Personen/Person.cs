using System.ComponentModel.DataAnnotations.Schema;
using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Data.Entities.Personen;

/// <summary>
/// Zentrale Personen-Akte – das Herzstück ab Phase 2. Bündelt Steckbrief, Foto-Galerie,
/// Verhör-/Maßnahmen-Doks und den Einstufungs-Verlauf. Voll auditiert und papierkorbfähig
/// (<see cref="IAuditable"/> + <see cref="ISoftDelete"/> → Audit-Log und Soft-Delete automatisch).
/// </summary>
public class Person : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Menschenlesbares, eindeutiges Aktenzeichen (z. B. NOOSE-P-2026-0001).</summary>
    public string Aktenzeichen { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Freitext-Beschreibung / Notizen zur Person.</summary>
    public string? Beschreibung { get; set; }

    public Lebensstatus Lebensstatus { get; set; } = Lebensstatus.Lebend;

    /// <summary>Zeitpunkt (UTC), bis zu dem ein „Tot"-Status gilt; danach effektiv wieder „Lebend".</summary>
    public DateTime? TotBis { get; set; }

    public Einstufung Einstufung { get; set; } = Einstufung.Unbekannt;

    /// <summary>Verschlusssache: in Liste/Detail nur für Führung/Admin sichtbar.</summary>
    public bool IstVerschlusssache { get; set; }

    /// <summary>
    /// Automatischer Bedrohungs-Score (0–100, <c>null</c> = noch nicht bewertet). Phase 8/Block D:
    /// berechnet &amp; persistiert vom <c>BedrohungsScoreService</c> (Algorithmus „EHK-Score", siehe AlgoPlan.md).
    /// Daraus wird on-read die <c>GefaehrdungsStufe</c> abgeleitet.
    /// </summary>
    public int? BedrohungsScore { get; set; }

    /// <summary>Daten-Konfidenz (0–100, <c>null</c> = nicht bewertet): wie gut die Person erfasst ist – getrennt
    /// vom Score (eine Lücke senkt den Score nie, nur die Konfidenz). Score immer mit Konfidenz-Badge anzeigen.</summary>
    public int? BedrohungsKonfidenz { get; set; }

    /// <summary>Strukturierte Aufschlüsselung des letzten Score-Laufs als JSON (Teilscores + Treiber + Band/Sockel),
    /// für die nachvollziehbare Anzeige „warum dieser Score?". Im selben Lauf wie der Score erzeugt (Konsistenz).</summary>
    public string? BedrohungsDetailJson { get; set; }

    /// <summary>Zeitpunkt der letzten Score-Berechnung (UTC); <c>null</c> = noch nie berechnet. Erlaubt dem
    /// nächtlichen Sweep, Decay-Drift zu erkennen, ohne die Aktualitäts-Ampel (<c>GeaendertAm</c>) zu verfälschen.</summary>
    public DateTime? ScoreBerechnetAm { get; set; }

    // ---- Steckbrief & Akteninhalt (Kind-Tabellen) ----
    public List<PersonAlias> Aliase { get; set; } = new();
    public List<PersonTelefon> Telefonnummern { get; set; } = new();
    public List<PersonFahrzeug> Fahrzeuge { get; set; } = new();
    public List<PersonOrt> Orte { get; set; } = new();
    public List<PersonWaffe> Waffen { get; set; } = new();
    public List<PersonFoto> Fotos { get; set; } = new();
    public List<PersonDok> Doks { get; set; } = new();
    public List<Observation> Observationen { get; set; } = new();
    // Der Einstufungs-Verlauf ist seit Phase 4 polymorph (EntitaetTyp/EntitaetId) und wird nicht
    // mehr als Navigation an der Person gehalten – er wird über den Dienst geladen.

    // ---- IAuditable ----
    public DateTime ErstelltAm { get; set; }
    public string? ErstelltVonId { get; set; }
    public DateTime? GeaendertAm { get; set; }
    public string? GeaendertVonId { get; set; }

    // ---- ISoftDelete ----
    public bool IstGeloescht { get; set; }
    public DateTime? GeloeschtAm { get; set; }
    public string? GeloeschtVonId { get; set; }

    /// <summary>
    /// Effektiver Lebensstatus inkl. temporärem Tod / Respawn. NICHT in DB-Abfragen verwenden
    /// (NotMapped) – in der Liste vorberechnen.
    /// </summary>
    [NotMapped]
    public Lebensstatus EffektiverLebensstatus
        => LebensstatusLogic.Effektiv(Lebensstatus, TotBis, DateTime.UtcNow);
}
