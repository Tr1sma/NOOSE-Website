using System.ComponentModel.DataAnnotations.Schema;
using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Data.Entities.Fraktionen;

/// <summary>
/// Eine Fraktion (Gang/Mafia/Konzern/…) als vollwertige Akte – Phase 4. Bündelt Stammdaten, strukturierte
/// Bestände (Waffen/Lager), eine Ränge-Liste und ihre Mitglieder (mit Fraktions-Rang). Voll auditiert und
/// papierkorbfähig (<see cref="IAuditable"/> + <see cref="ISoftDelete"/>). Konflikte zu anderen Fraktionen/
/// Parteien laufen über die generische Verknüpfungs-Engine.
/// </summary>
[Table("Fraktionen")]
public class Fraktion : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Menschenlesbares, eindeutiges Aktenzeichen (z. B. NOOSE-F-2026-0001).</summary>
    [Column("Aktenzeichen")]
    public string Aktenzeichen { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Art der Fraktion (Freitext, z. B. „Gang", „Motorradclub", „Konzern").</summary>
    [Column("Art")]
    public string? Art { get; set; }

    [Column("Funk")]
    public string? Funk { get; set; }
    public string? Darkchat { get; set; }
    [Column("Ausstellungszeiten")]
    public string? Ausstellungszeiten { get; set; }

    /// <summary>Anwesen/Sitz der Fraktion als Freitext (z. B. Adresse + Zugangsnotizen).</summary>
    [Column("Anwesen")]
    public string? Anwesen { get; set; }

    /// <summary>Erkennungsfarbe als Hex-Code (z. B. #1E88E5).</summary>
    [Column("Erkennungsfarbe")]
    public string? Erkennungsfarbe { get; set; }

    [Column("Ziele")]
    public string? Ziele { get; set; }
    [Column("Beschreibung")]
    public string? Beschreibung { get; set; }

    /// <summary>Optionale Einstufung der Fraktion.</summary>
    [Column("Einstufung")]
    public Einstufung Einstufung { get; set; } = Einstufung.Unbekannt;

    /// <summary>
    /// Automatischer Bedrohungs-Score (0–100, <c>null</c> = noch nicht bewertet bzw. ausgenommen, z. B.
    /// Staatsfraktion). Phase 8/Block D: berechnet &amp; persistiert vom <c>BedrohungsScoreService</c>
    /// (Algorithmus „EHK-Score", siehe AlgoPlan.md). Daraus wird on-read die <c>GefaehrdungsStufe</c> abgeleitet.
    /// </summary>
    [Column("BedrohungsScore")]
    public int? BedrohungsScore { get; set; }

    /// <summary>Daten-Konfidenz (0–100, <c>null</c> = nicht bewertet): wie gut die Fraktion erfasst ist – getrennt
    /// vom Score (eine Lücke senkt den Score nie, nur die Konfidenz). Score immer mit Konfidenz-Badge anzeigen.</summary>
    [Column("BedrohungsKonfidenz")]
    public int? BedrohungsKonfidenz { get; set; }

    /// <summary>Strukturierte Aufschlüsselung des letzten Score-Laufs als JSON (Teilscores + Treiber + Band/Sockel),
    /// für die nachvollziehbare Anzeige „warum dieser Score?". Im selben Lauf wie der Score erzeugt (Konsistenz).</summary>
    [Column("BedrohungsDetailJson")]
    public string? BedrohungsDetailJson { get; set; }

    /// <summary>Zeitpunkt der letzten Score-Berechnung (UTC); <c>null</c> = noch nie berechnet. Erlaubt dem
    /// nächtlichen Sweep, Decay-Drift zu erkennen, ohne die Aktualitäts-Ampel (<c>GeaendertAm</c>) zu verfälschen.</summary>
    [Column("ScoreBerechnetAm")]
    public DateTime? ScoreBerechnetAm { get; set; }

    /// <summary>Verschlusssache: in Liste/Detail nur für Führung/Admin sichtbar.</summary>
    [Column("IstVerschlusssache")]
    public bool IstVerschlusssache { get; set; }

    /// <summary>Staatsfraktion: kann nicht „veraltet" werden (Aktualitäts-Ampel bleibt dauerhaft „Aktuell").</summary>
    [Column("IstStaatsfraktion")]
    public bool IstStaatsfraktion { get; set; }

    /// <summary>Geschätzte Gesamtgröße der Fraktion (= y im Erfassungsfortschritt x/y); optional. Wie bei der Personengruppe.</summary>
    [Column("GeschaetzteMitgliederzahl")]
    public int? GeschaetzteMitgliederzahl { get; set; }

    // ---- Kind-Tabellen ----
    public List<FraktionRang> Raenge { get; set; } = new();
    public List<FraktionWaffenbestand> Waffenbestand { get; set; } = new();
    public List<FraktionLagerbestand> Lagerbestand { get; set; } = new();
    public List<FraktionDrogenroute> Drogenrouten { get; set; } = new();
    public List<FraktionMitglied> Mitglieder { get; set; } = new();
    public List<FraktionAgent> Agenten { get; set; } = new();

    /// <summary>Fotos der Fraktion; eines kann als Titelbild markiert sein (<see cref="FraktionFoto.IstTitelbild"/>).</summary>
    public List<FraktionFoto> Fotos { get; set; } = new();

    /// <summary>Aktivitäten/Aktionen der Fraktion für den Zeitstrahl (z. B. Raub, Geiselnahme).</summary>
    public List<FraktionAktivitaet> Aktivitaeten { get; set; } = new();

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
