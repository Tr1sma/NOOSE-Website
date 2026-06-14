using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Taskforces;

/// <summary>
/// Eine Taskforce als vollwertige Akte – Phase 5c. NOOSE-interne (oder behördenübergreifende) Einsatzgruppe:
/// Name, Sinn/Zweck (<see cref="Zweck"/>), <see cref="Geltungsbereich"/> (inner-/überbehördlich) und ein
/// Genehmigungs-<see cref="Status"/>. Mitglieder und Leitung sind <b>Agents</b> (NOOSE-Nutzer) über die
/// Join-Tabelle <see cref="TaskforceAgent"/> – es gibt bewusst <i>keine</i> Personen-Mitglieder und (anders als
/// die Verdächtigen-Akten) <i>keine</i> Einstufung. Voll auditiert und papierkorbfähig
/// (<see cref="IAuditable"/> + <see cref="ISoftDelete"/>).
/// </summary>
[Table("Taskforces")]
public class Taskforce : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Menschenlesbares, eindeutiges Aktenzeichen (z. B. NOOSE-TF-2026-0001).</summary>
    [Column("Aktenzeichen")]
    public string CaseNumber { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Sinn/Zweck der Taskforce (Freitext).</summary>
    [Column("Zweck")]
    public string? Purpose { get; set; }

    [Column("Geltungsbereich")]
    public TaskforceScope Scope { get; set; } = TaskforceScope.InternalAgency;

    /// <summary>Genehmigungs-/Lebenszyklus-Status. Beim Anlegen stets <see cref="TaskforceStatus.Beantragt"/>.</summary>
    public TaskforceStatus Status { get; set; } = TaskforceStatus.Requested;

    /// <summary>Interne Bemerkungen/Vermerke (Freitext).</summary>
    [Column("Bemerkungen")]
    public string? Remarks { get; set; }

    /// <summary>Verschlusssache: in Liste/Detail nur für Führung/Admin sichtbar.</summary>
    [Column("IstVerschlusssache")]
    public bool IsClassified { get; set; }

    // ---- Kind-Tabellen ----
    public List<TaskforceAgent> Agents { get; set; } = new();

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
