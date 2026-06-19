using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Recruiting;

/// <summary>An answer option for a multiple-choice question; IsCorrect is an internal scoring aid for HRB.</summary>
[Table("BewerbungTestOptionen")]
public class BewerbungTestOption : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string QuestionId { get; set; } = string.Empty;
    public BewerbungTestQuestion? Question { get; set; }

    [Column("Text")]
    public string Label { get; set; } = string.Empty;

    [Column("IstRichtig")]
    public bool IsCorrect { get; set; }

    [Column("Sortierung")]
    public int Sorting { get; set; }

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
