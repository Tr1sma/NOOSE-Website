using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Cases;

/// <summary>
/// Zuteilung eines NOOSE-Agents zu einem Vorgang (beteiligte Ermittlungskraft). Join-Entity mit
/// <see cref="IAuditable"/>. FK auf den <see cref="Agent"/> (Identity-Tabelle) ist <c>Restrict</c>; FK auf
/// den Vorgang ist Cascade. Vorlage: <see cref="Operationen.OperationAgent"/>.
/// </summary>
[Table("VorgangAgenten")]
public class CaseAgent : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("VorgangId")]
    public string CaseId { get; set; } = string.Empty;
    public Case? Case { get; set; }

    public string AgentId { get; set; } = string.Empty;
    public Agent? Agent { get; set; }

    /// <summary>Markiert diesen zugeteilten Agent als Fallführer des Vorgangs (mehrere je Akte möglich).</summary>
    [Column("IstFallfuehrer")]
    public bool IsCaseLead { get; set; }

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
