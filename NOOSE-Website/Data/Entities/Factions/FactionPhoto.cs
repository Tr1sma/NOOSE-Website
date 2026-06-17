using System.ComponentModel.DataAnnotations.Schema;
namespace NOOSE_Website.Data.Entities.Factions;

/// <summary>Metadata for a faction gallery photo; the file lives outside wwwroot. FileNameSaved is server-assigned to prevent path traversal. At most one photo per faction is the title image (enforced transactionally).</summary>
[Table("FraktionFotos")]
public class FactionPhoto
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    [Column("FraktionId")]
    public string FactionId { get; set; } = string.Empty;
    public Faction? Faction { get; set; }
    [Column("DateinameGespeichert")]
    public string FileNameSaved { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    [Column("GroesseBytes")]
    public long SizeBytes { get; set; }

    /// <summary>Marked as the faction title image (at most one per faction).</summary>
    [Column("IstTitelbild")]
    public bool IsTitleImage { get; set; }

    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
}
