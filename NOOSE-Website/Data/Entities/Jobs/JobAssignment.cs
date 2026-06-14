using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Jobs;

/// <summary>
/// Zuweisung einer Aufgabe an einen NOOSE-Agent – Phase 6. Join-Entity mit <see cref="IAuditable"/> (flache
/// Zuweisung, keine Leitungs-Markierung). FK auf den <see cref="Agent"/> (Identity-Tabelle) ist <c>Restrict</c>;
/// FK auf die Aufgabe ist Cascade. Vorlage: <see cref="Vorgaenge.VorgangAgent"/>.
/// </summary>
[Table("AufgabeZuweisungen")]
public class JobAssignment : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("AufgabeId")]
    public string JobId { get; set; } = string.Empty;
    public Job? Job { get; set; }

    public string AgentId { get; set; } = string.Empty;
    public Agent? Agent { get; set; }

    // ---- IAuditable ----
    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }
}
