using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.People;

/// <summary>Typed relation between two persons. No collection navigation: two FKs to the same table would collide; load via PersonAId == id || PersonBId == id. FK Restrict avoids cascade-path conflicts.</summary>
[Table("PersonBeziehungen")]
public class PersonRelation : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string PersonAId { get; set; } = string.Empty;
    public Person? PersonA { get; set; }

    public string PersonBId { get; set; } = string.Empty;
    public Person? PersonB { get; set; }

    [Column("Typ")]
    public RelationType Type { get; set; }

    [Column("Notiz")]
    public string? Note { get; set; }

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
