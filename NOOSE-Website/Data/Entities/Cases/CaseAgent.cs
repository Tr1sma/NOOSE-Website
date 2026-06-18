using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Cases;

/// <summary>Assignment of an agent to a case. FK to Agent is Restrict; FK to Case is Cascade.</summary>
[Table("VorgangAgenten")]
public class CaseAgent : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("VorgangId")]
    public string CaseId { get; set; } = string.Empty;
    public Case? Case { get; set; }

    public string AgentId { get; set; } = string.Empty;
    public Agent? Agent { get; set; }

    /// <summary>Marks this agent as a case lead (multiple allowed per case).</summary>
    [Column("IstFallfuehrer")]
    public bool IsCaseLead { get; set; }

    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }
}
