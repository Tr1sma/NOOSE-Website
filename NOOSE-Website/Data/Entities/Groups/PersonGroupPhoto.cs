using System.ComponentModel.DataAnnotations.Schema;
using NOOSE_Website.Models.Abstractions;
namespace NOOSE_Website.Data.Entities.Groups;

/// <summary>Metadata for a person-group gallery photo; the file lives outside wwwroot. FileNameSaved is server-assigned to prevent path traversal. At most one photo per group is the title image (enforced transactionally).</summary>
[Table("PersonengruppeFotos")]
public class PersonGroupPhoto : IAuditable, ISoftDelete, IRecordPhoto
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    [Column("PersonengruppeId")]
    public string PersonGroupId { get; set; } = string.Empty;
    public PersonGroup? PersonGroup { get; set; }
    [Column("DateinameGespeichert")]
    public string FileNameSaved { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    [Column("GroesseBytes")]
    public long SizeBytes { get; set; }

    /// <summary>Marked as the group title image (at most one per group).</summary>
    [Column("IstTitelbild")]
    public bool IsTitleImage { get; set; }

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
