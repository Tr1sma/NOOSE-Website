using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Personnel;

/// <summary>Marks a training module complete for an agent; uniqueness enforced by the service (no unique index, so re-completion after removal stays possible).</summary>
[Table("AgentModulAbschluesse")]
public class AgentModuleCompletion : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string AgentId { get; set; } = string.Empty;

    [Column("ModulId")]
    public string ModuleId { get; set; } = string.Empty;

    [Column("AbgeschlossenAm")]
    public DateTime CompletedAt { get; set; }

    [Column("ErfasstVonName")]
    public string? CompleterName { get; set; }

    [Column("Notiz")]
    public string? Note { get; set; }

    public TrainingModule? Module { get; set; }

    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }

    [Column("IstGeloescht")]
    public bool IsDeleted { get; set; }
    [Column("GeloeschtAm")]
    public DateTime? DeletedAt { get; set; }
    [Column("GeloeschtVonId")]
    public string? DeletedById { get; set; }
}
