using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Jobs;

/// <summary>Assignment of a job to a NOOSE agent.</summary>
[Table("AufgabeZuweisungen")]
public class JobAssignment : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("AufgabeId")]
    public string JobId { get; set; } = string.Empty;
    public Job? Job { get; set; }

    public string AgentId { get; set; } = string.Empty;
    public Agent? Agent { get; set; }

    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }
}
