using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Aufgaben;

/// <summary>
/// Eine Aufgabe/To-Do – Phase 6. Vollwertige, verknüpfbare Akte (Team-Board: für alle aktiven Agenten sichtbar,
/// daher <b>ohne</b> Verschlusssache/Einstufung – anders als <see cref="Vorgaenge.Vorgang"/>). Kann an mehrere Agenten
/// zugewiesen werden (<see cref="AufgabeZuweisung"/>) und über die generische Verknüpfungs-Engine mit beliebigen Akten
/// verknüpft werden. Voll auditiert und papierkorbfähig (<see cref="IAuditable"/> + <see cref="ISoftDelete"/>).
/// <c>ErstelltVonId</c> ist der Ersteller.
/// </summary>
[Table("Aufgaben")]
public class Aufgabe : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Menschenlesbares, eindeutiges Aktenzeichen (z. B. NOOSE-A-2026-0001).</summary>
    [Column("Aktenzeichen")]
    public string Aktenzeichen { get; set; } = string.Empty;

    [Column("Titel")]
    public string Titel { get; set; } = string.Empty;

    /// <summary>Beschreibung/Worum geht es (Freitext).</summary>
    [Column("Beschreibung")]
    public string? Beschreibung { get; set; }

    public AufgabeStatus Status { get; set; } = AufgabeStatus.Offen;

    [Column("Prioritaet")]
    public AufgabePrioritaet Prioritaet { get; set; } = AufgabePrioritaet.Normal;

    /// <summary>Fälligkeitsdatum (optional). Überfällig = in der Vergangenheit bei noch offenem Status.</summary>
    [Column("Faelligkeit")]
    public DateTime? Faelligkeit { get; set; }

    /// <summary>Zeitpunkt des Abschlusses – gesetzt, sobald der Status auf Erledigt/Abgebrochen wechselt.</summary>
    [Column("ErledigtAm")]
    public DateTime? ErledigtAm { get; set; }

    /// <summary>
    /// Eingeschränkt: nur zugeteilte Agenten, der Ersteller sowie die Aufsicht (Führung/Admin/Teamleitung,
    /// d. h. <c>DarfVerschlusssacheLesen()</c>) sehen die Aufgabe. Nicht gesetzt = für alle sichtbar (Team-Board).
    /// </summary>
    [Column("IstEingeschraenkt")]
    public bool IstEingeschraenkt { get; set; }

    // ---- Kind-Tabellen ----
    public List<AufgabeZuweisung> Zuweisungen { get; set; } = new();

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
