using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Termine;

/// <summary>
/// Ein Termin (Gerichtstermin, Besprechung, Frist …) als vollwertige, verknüpfbare Akte – Phase 8 (Block C).
/// Frei anlegbarer Kalendereintrag mit Zeitraum (<see cref="Beginn"/>/<see cref="Ende"/>) und Teilnehmern
/// (<see cref="TerminZuweisung"/>). Sichtbarkeit über drei Stufen (<see cref="Sichtbarkeit"/>): Öffentlich
/// (alle aktiven Agenten, Behörden-Kalender), Eingeschränkt (Ersteller + zugeteilte Teilnehmer + Aufsicht) und
/// Privat (nur der Ersteller + Aufsicht). Die Aufsicht/Führung (<c>DarfVerschlusssacheLesen()</c>) sieht alle
/// Stufen. KEIN Verschlusssache-/Einstufungs-Konzept (anders als <see cref="Operationen.Operation"/>). Voll
/// auditiert und papierkorbfähig (<see cref="IAuditable"/> + <see cref="ISoftDelete"/>). <c>ErstelltVonId</c>
/// ist der Ersteller.
/// </summary>
[Table("Termine")]
public class Termin : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Menschenlesbares, eindeutiges Aktenzeichen (z. B. NOOSE-TM-2026-0001).</summary>
    [Column("Aktenzeichen")]
    public string Aktenzeichen { get; set; } = string.Empty;

    [Column("Titel")]
    public string Titel { get; set; } = string.Empty;

    [Column("Kategorie")]
    public TerminKategorie Kategorie { get; set; } = TerminKategorie.Sonstiges;

    public TerminStatus Status { get; set; } = TerminStatus.Geplant;

    /// <summary>Ort des Termins (Freitext).</summary>
    [Column("Ort")]
    public string? Ort { get; set; }

    /// <summary>Beginn des Termins (RP-Zeit, UTC gespeichert). Pflichtfeld.</summary>
    [Column("Beginn")]
    public DateTime Beginn { get; set; }

    /// <summary>Ende des Termins (optional, RP-Zeit, UTC gespeichert).</summary>
    [Column("Ende")]
    public DateTime? Ende { get; set; }

    /// <summary>Ganztägiger Termin – Uhrzeiten werden dann ausgeblendet/ignoriert.</summary>
    [Column("Ganztaegig")]
    public bool Ganztaegig { get; set; }

    /// <summary>Beschreibung/Worum geht es (Freitext).</summary>
    [Column("Beschreibung")]
    public string? Beschreibung { get; set; }

    /// <summary>
    /// Sichtbarkeitsstufe: Öffentlich (alle, Behörden-Kalender), Eingeschränkt (Ersteller + Teilnehmer +
    /// Aufsicht) oder Privat (nur Ersteller + Aufsicht). Die Aufsicht/Führung (<c>DarfVerschlusssacheLesen()</c>)
    /// sieht alle Stufen.
    /// </summary>
    [Column("Sichtbarkeit")]
    public TerminSichtbarkeitsStufe Sichtbarkeit { get; set; } = TerminSichtbarkeitsStufe.Oeffentlich;

    // ---- Kind-Tabellen ----
    public List<TerminZuweisung> Teilnehmer { get; set; } = new();

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
