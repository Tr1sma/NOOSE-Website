using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Operations;

/// <summary>Assignment of a NOOSE agent to an operation.</summary>
[Table("OperationAgenten")]
public class OperationAgent : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string OperationId { get; set; } = string.Empty;
    public Operation? Operation { get; set; }

    public string AgentId { get; set; } = string.Empty;
    public Agent? Agent { get; set; }

    /// <summary>Marks this agent as investigation lead (multiple allowed per file).</summary>
    [Column("IstErmittlungsleiter")]
    public bool IsInvestigationLead { get; set; }

    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }
}
