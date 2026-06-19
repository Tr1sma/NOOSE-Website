using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Recruiting;

/// <summary>An applicant's answer to one test question (selected option and/or free text).</summary>
[Table("BewerbungTestAntworten")]
public class BewerbungTestAnswer : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string AssignmentId { get; set; } = string.Empty;
    public BewerbungTestAssignment? Assignment { get; set; }

    public string QuestionId { get; set; } = string.Empty;
    public BewerbungTestQuestion? Question { get; set; }

    [Column("OptionId")]
    public string? SelectedOptionId { get; set; }

    [Column("Freitext")]
    public string? FreeTextAnswer { get; set; }

    /// <summary>HRB manual grade override; null = auto result applies.</summary>
    [Column("ManuellRichtig")]
    public bool? ManualCorrect { get; set; }

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
