using System.ComponentModel.DataAnnotations.Schema;
namespace NOOSE_Website.Data.Entities.People;

/// <summary>Metadata for a person-gallery photo; the file lives outside wwwroot. FileNameSaved is server-assigned to prevent path traversal.</summary>
[Table("PersonFotos")]
public class PersonPhoto
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
}
