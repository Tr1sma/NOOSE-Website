using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Operations;

/// <summary>
/// Zuteilung eines NOOSE-Agents zu einer Operation (beteiligte Einsatzkraft). Join-Entity mit
/// <see cref="IAuditable"/>. FK auf den <see cref="Agent"/> (Identity-Tabelle) ist <c>Restrict</c>; FK auf
/// die Operation ist Cascade.
/// </summary>
[Table("OperationAgenten")]
public class OperationAgent : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string OperationId { get; set; } = string.Empty;
    public Operation? Operation { get; set; }

    public string AgentId { get; set; } = string.Empty;
    public Agent? Agent { get; set; }

    /// <summary>Markiert diesen zugeteilten Agent als Ermittlungsleiter der Operation (mehrere je Akte möglich).</summary>
    [Column("IstErmittlungsleiter")]
    public bool IsInvestigationLead { get; set; }

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
