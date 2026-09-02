using System.ComponentModel.DataAnnotations.Schema;
using NOOSE_Website.Models.Abstractions;
namespace NOOSE_Website.Data.Entities.Parties;

/// <summary>Metadata for a party gallery photo; the file lives outside wwwroot. FileNameSaved is server-assigned to prevent path traversal. At most one photo per party is the title image (enforced transactionally).</summary>
[Table("ParteiFotos")]
public class PartyPhoto : IAuditable, ISoftDelete, IRecordPhoto
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    [Column("ParteiId")]
    public string PartyId { get; set; } = string.Empty;
    public Party? Party { get; set; }
    [Column("DateinameGespeichert")]
    public string FileNameSaved { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    [Column("GroesseBytes")]
    public long SizeBytes { get; set; }

    /// <summary>Marked as the party title image (at most one per party).</summary>
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
