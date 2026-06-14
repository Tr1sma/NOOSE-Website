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
    public string Aktenzeichen { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Sinn/Zweck der Taskforce (Freitext).</summary>
    [Column("Zweck")]
    public string? Zweck { get; set; }

    [Column("Geltungsbereich")]
    public TaskforceGeltungsbereich Geltungsbereich { get; set; } = TaskforceGeltungsbereich.Innerbehoerdlich;

    /// <summary>Genehmigungs-/Lebenszyklus-Status. Beim Anlegen stets <see cref="TaskforceStatus.Beantragt"/>.</summary>
    public TaskforceStatus Status { get; set; } = TaskforceStatus.Beantragt;

    /// <summary>Interne Bemerkungen/Vermerke (Freitext).</summary>
    [Column("Bemerkungen")]
    public string? Bemerkungen { get; set; }

    /// <summary>Verschlusssache: in Liste/Detail nur für Führung/Admin sichtbar.</summary>
    [Column("IstVerschlusssache")]
    public bool IstVerschlusssache { get; set; }

    // ---- Kind-Tabellen ----
    public List<TaskforceAgent> Agenten { get; set; } = new();

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
