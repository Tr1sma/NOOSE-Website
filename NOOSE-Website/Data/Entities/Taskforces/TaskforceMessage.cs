using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Taskforces;

/// <summary>Taskforce team-chat message; visibility inherited from the parent taskforce.</summary>
[Table("TaskforceNachrichten")]
public class TaskforceMessage : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string TaskforceId { get; set; } = string.Empty;
    public Taskforce? Taskforce { get; set; }

    /// <summary>Raw text incl. inline @{Type:Id} mention tokens.</summary>
    public string Text { get; set; } = string.Empty;

    [Column("AutorName")]
    public string? AuthorName { get; set; }

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
