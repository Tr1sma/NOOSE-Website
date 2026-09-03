using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Recruiting;

/// <summary>Assigns one test to one application; the applicant fills it in the portal.</summary>
[Table("BewerbungTestZuweisungen")]
public class BewerbungTestAssignment : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string BewerbungId { get; set; } = string.Empty;
    public Bewerbung? Bewerbung { get; set; }

    public string TestId { get; set; } = string.Empty;
    public BewerbungTest? Test { get; set; }

    [Column("ZugewiesenVon")]
    public string? AssignedByName { get; set; }

    [Column("AbgeschlossenAm")]
    public DateTime? CompletedAt { get; set; }

    /// <summary>When the grading was declared finished; null while questions are still being marked.</summary>
    [Column("BewertetAm")]
    public DateTime? GradedAt { get; set; }

    [Column("BewertetVon")]
    public string? GradedByName { get; set; }

    /// <summary>Result frozen at that moment; without it a later edit to the test rewrites an old verdict.</summary>
    [Column("ErgebnisPunkte")]
    public int? FinalPoints { get; set; }

    [Column("ErgebnisMaxPunkte")]
    public int? FinalMaxPoints { get; set; }

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
