using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Groups;

/// <summary>Assignment of a NOOSE agent to a person group.</summary>
[Table("PersonengruppeAgenten")]
public class PersonGroupAgent : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("PersonengruppeId")]
    public string PersonGroupId { get; set; } = string.Empty;
    public PersonGroup? PersonGroup { get; set; }

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
