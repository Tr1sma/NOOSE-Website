using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Data.Entities.Termine;

/// <summary>
/// Ein Termin (Gerichtstermin, Besprechung, Frist …) als vollwertige, verknüpfbare Akte – Phase 8 (Block C).
/// Frei anlegbarer Kalendereintrag mit Zeitraum (<see cref="Beginn"/>/<see cref="Ende"/>) und Teilnehmern
/// (<see cref="TerminZuweisung"/>). Sichtbarkeit wie eine Aufgabe (Team-Board): nicht eingeschränkt = für alle
/// aktiven Agenten sichtbar; <see cref="IstEingeschraenkt"/> = nur der Ersteller, zugeteilte Teilnehmer und die
/// Aufsicht (<c>DarfVerschlusssacheLesen()</c>). KEIN Verschlusssache-/Einstufungs-Konzept (anders als
/// <see cref="Operationen.Operation"/>). Voll auditiert und papierkorbfähig (<see cref="IAuditable"/> +
/// <see cref="ISoftDelete"/>). <c>ErstelltVonId</c> ist der Ersteller.
/// </summary>
public class Termin : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Menschenlesbares, eindeutiges Aktenzeichen (z. B. NOOSE-TM-2026-0001).</summary>
    public string Aktenzeichen { get; set; } = string.Empty;

    public string Titel { get; set; } = string.Empty;

    public TerminKategorie Kategorie { get; set; } = TerminKategorie.Sonstiges;

    public TerminStatus Status { get; set; } = TerminStatus.Geplant;

    /// <summary>Ort des Termins (Freitext).</summary>
    public string? Ort { get; set; }

    /// <summary>Beginn des Termins (RP-Zeit, UTC gespeichert). Pflichtfeld.</summary>
    public DateTime Beginn { get; set; }

    /// <summary>Ende des Termins (optional, RP-Zeit, UTC gespeichert).</summary>
    public DateTime? Ende { get; set; }

    /// <summary>Ganztägiger Termin – Uhrzeiten werden dann ausgeblendet/ignoriert.</summary>
    public bool Ganztaegig { get; set; }

    /// <summary>Beschreibung/Worum geht es (Freitext).</summary>
    public string? Beschreibung { get; set; }

    /// <summary>
    /// Eingeschränkt: nur zugeteilte Teilnehmer, der Ersteller sowie die Aufsicht (Führung/Admin/Teamleitung,
    /// d. h. <c>DarfVerschlusssacheLesen()</c>) sehen den Termin. Nicht gesetzt = für alle sichtbar.
    /// </summary>
    public bool IstEingeschraenkt { get; set; }

    // ---- Kind-Tabellen ----
    public List<TerminZuweisung> Teilnehmer { get; set; } = new();

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
