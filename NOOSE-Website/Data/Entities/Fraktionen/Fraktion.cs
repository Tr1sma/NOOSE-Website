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
public class Fraktion : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Menschenlesbares, eindeutiges Aktenzeichen (z. B. NOOSE-F-2026-0001).</summary>
    public string Aktenzeichen { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Art der Fraktion (Freitext, z. B. „Gang", „Motorradclub", „Konzern").</summary>
    public string? Art { get; set; }

    public string? Funk { get; set; }
    public string? Darkchat { get; set; }
    public string? Ausstellungszeiten { get; set; }

    /// <summary>Anwesen/Sitz der Fraktion als Freitext (z. B. Adresse + Zugangsnotizen).</summary>
    public string? Anwesen { get; set; }

    /// <summary>Erkennungsfarbe als Hex-Code (z. B. #1E88E5).</summary>
    public string? Erkennungsfarbe { get; set; }

    public string? Ziele { get; set; }
    public string? Beschreibung { get; set; }

    /// <summary>Optionale Einstufung der Fraktion.</summary>
    public Einstufung Einstufung { get; set; } = Einstufung.Unbekannt;

    /// <summary>Automatischer Bedrohungs-Score – nur modelliert, Berechnung folgt in Phase 8.</summary>
    public int? BedrohungsScore { get; set; }

    /// <summary>Verschlusssache: in Liste/Detail nur für Führung/Admin sichtbar.</summary>
    public bool IstVerschlusssache { get; set; }

    // ---- Kind-Tabellen ----
    public List<FraktionRang> Raenge { get; set; } = new();
    public List<FraktionWaffenbestand> Waffenbestand { get; set; } = new();
    public List<FraktionLagerbestand> Lagerbestand { get; set; } = new();
    public List<FraktionMitglied> Mitglieder { get; set; } = new();
    public List<FraktionAgent> Agenten { get; set; } = new();

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
