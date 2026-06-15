using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Jobs;

/// <summary>
/// Eine Aufgabe/To-Do – Phase 6. Vollwertige, verknüpfbare Akte (Team-Board: für alle aktiven Agenten sichtbar,
/// daher <b>ohne</b> Verschlusssache/Einstufung – anders als <see cref="Vorgaenge.Vorgang"/>). Kann an mehrere Agenten
/// zugewiesen werden (<see cref="AufgabeZuweisung"/>) und über die generische Verknüpfungs-Engine mit beliebigen Akten
/// verknüpft werden. Voll auditiert und papierkorbfähig (<see cref="IAuditable"/> + <see cref="ISoftDelete"/>).
/// <c>ErstelltVonId</c> ist der Ersteller.
/// </summary>
[Table("Aufgaben")]
public class Job : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Menschenlesbares, eindeutiges Aktenzeichen (z. B. NOOSE-A-2026-0001).</summary>
    [Column("Aktenzeichen")]
    public string CaseNumber { get; set; } = string.Empty;

    [Column("Titel")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Beschreibung/Worum geht es (Freitext).</summary>
    [Column("Beschreibung")]
    public string? Description { get; set; }

    public JobStatus Status { get; set; } = JobStatus.Open;

    [Column("Prioritaet")]
    public JobPriority Priority { get; set; } = JobPriority.Normal;

    /// <summary>Fälligkeitsdatum (optional). Überfällig = in der Vergangenheit bei noch offenem Status.</summary>
    [Column("Faelligkeit")]
    public DateTime? DueDate { get; set; }

    /// <summary>Zeitpunkt des Abschlusses – gesetzt, sobald der Status auf Erledigt/Abgebrochen wechselt.</summary>
    [Column("ErledigtAm")]
    public DateTime? DoneAt { get; set; }

    /// <summary>
    /// Eingeschränkt: nur zugeteilte Agenten, der Ersteller sowie die Aufsicht (Führung/Admin/Teamleitung,
    /// d. h. <c>DarfVerschlusssacheLesen()</c>) sehen die Aufgabe. Nicht gesetzt = für alle sichtbar (Team-Board).
    /// </summary>
    [Column("IstEingeschraenkt")]
    public bool IsRestricted { get; set; }

    // ---- Kind-Tabellen ----
    public List<JobAssignment> Assignments { get; set; } = new();

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
}
