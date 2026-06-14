using System.ComponentModel.DataAnnotations.Schema;
using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Data.Entities.People;

/// <summary>
/// Zentrale Personen-Akte – das Herzstück ab Phase 2. Bündelt Steckbrief, Foto-Galerie,
/// Verhör-/Maßnahmen-Doks und den Einstufungs-Verlauf. Voll auditiert und papierkorbfähig
/// (<see cref="IAuditable"/> + <see cref="ISoftDelete"/> → Audit-Log und Soft-Delete automatisch).
/// </summary>
[Table("Personen")]
public class Person : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Menschenlesbares, eindeutiges Aktenzeichen (z. B. NOOSE-P-2026-0001).</summary>
    [Column("Aktenzeichen")]
    public string CaseNumber { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Freitext-Beschreibung / Notizen zur Person.</summary>
    [Column("Beschreibung")]
    public string? Description { get; set; }

    [Column("Lebensstatus")]
    public LifeStatus LifeStatus { get; set; } = LifeStatus.Alive;

    /// <summary>Zeitpunkt (UTC), bis zu dem ein „Tot"-Status gilt; danach effektiv wieder „Lebend".</summary>
    [Column("TotBis")]
    public DateTime? DeadUntil { get; set; }

    [Column("Einstufung")]
    public Classification Classification { get; set; } = Classification.Unknown;

    /// <summary>Verschlusssache: in Liste/Detail nur für Führung/Admin sichtbar.</summary>
    [Column("IstVerschlusssache")]
    public bool IsClassified { get; set; }

    /// <summary>
    /// Automatischer Bedrohungs-Score (0–100, <c>null</c> = noch nicht bewertet). Phase 8/Block D:
    /// berechnet &amp; persistiert vom <c>BedrohungsScoreService</c> (Algorithmus „EHK-Score", siehe AlgoPlan.md).
    /// Daraus wird on-read die <c>GefaehrdungsStufe</c> abgeleitet.
    /// </summary>
    [Column("BedrohungsScore")]
    public int? ThreatScore { get; set; }

    /// <summary>Daten-Konfidenz (0–100, <c>null</c> = nicht bewertet): wie gut die Person erfasst ist – getrennt
    /// vom Score (eine Lücke senkt den Score nie, nur die Konfidenz). Score immer mit Konfidenz-Badge anzeigen.</summary>
    [Column("BedrohungsKonfidenz")]
    public int? ThreatConfidence { get; set; }

    /// <summary>Strukturierte Aufschlüsselung des letzten Score-Laufs als JSON (Teilscores + Treiber + Band/Sockel),
    /// für die nachvollziehbare Anzeige „warum dieser Score?". Im selben Lauf wie der Score erzeugt (Konsistenz).</summary>
    [Column("BedrohungsDetailJson")]
    public string? ThreatDetailJson { get; set; }

    /// <summary>Zeitpunkt der letzten Score-Berechnung (UTC); <c>null</c> = noch nie berechnet. Erlaubt dem
    /// nächtlichen Sweep, Decay-Drift zu erkennen, ohne die Aktualitäts-Ampel (<c>GeaendertAm</c>) zu verfälschen.</summary>
    [Column("ScoreBerechnetAm")]
    public DateTime? ScoreCalculatedAt { get; set; }

    // ---- Steckbrief & Akteninhalt (Kind-Tabellen) ----
    public List<PersonAlias> Aliases { get; set; } = new();
    public List<PersonPhone> PhoneNumbers { get; set; } = new();
    public List<PersonVehicle> Vehicles { get; set; } = new();
    public List<PersonLocation> Locations { get; set; } = new();
    public List<PersonWeapon> Weapons { get; set; } = new();
    public List<PersonPhoto> Photos { get; set; } = new();
    public List<PersonDoc> Docs { get; set; } = new();
    public List<Observation> Observations { get; set; } = new();
    // Der Einstufungs-Verlauf ist seit Phase 4 polymorph (EntitaetTyp/EntitaetId) und wird nicht
    // mehr als Navigation an der Person gehalten – er wird über den Dienst geladen.

    // ---- IAuditable ----
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

    /// <summary>
    /// Effektiver Lebensstatus inkl. temporärem Tod / Respawn. NICHT in DB-Abfragen verwenden
    /// (NotMapped) – in der Liste vorberechnen.
    /// </summary>
    [NotMapped]
    public LifeStatus EffectiveLifeStatus
        => LifeStatusLogic.Effective(LifeStatus, DeadUntil, DateTime.UtcNow);
}
