using System.ComponentModel.DataAnnotations.Schema;
using NOOSE_Website.Models.Abstractions;
namespace NOOSE_Website.Data.Entities.People;

/// <summary>Metadata for a person-gallery photo; the file lives outside wwwroot. FileNameSaved is server-assigned to prevent path traversal.</summary>
[Table("PersonFotos")]
public class PersonPhoto : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PersonId { get; set; } = string.Empty;
    public Person? Person { get; set; }
    [Column("DateinameGespeichert")]
    public string FileNameSaved { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    [Column("GroesseBytes")]
    public long SizeBytes { get; set; }
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
    [Column("FokuspunktX")]
    public int FocalPointX { get; set; } = 50;
    [Column("FokuspunktY")]
    public int FocalPointY { get; set; } = 25;
}
