using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Parties;

/// <summary>Person's membership in a party; soft-delete preserves the membership history (deleted = ended).</summary>
[Table("ParteiMitglieder")]
public class PartyMember : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("ParteiId")]
    public string PartyId { get; set; } = string.Empty;
    public Party? Party { get; set; }

    public string PersonId { get; set; } = string.Empty;
    public Person? Person { get; set; }

    [Column("Rolle")]
    public string? Role { get; set; }

    [Column("IstLeitung")]
    public bool IsLead { get; set; }

    // CreatedAt = join date
    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }

    // DeletedAt = end/leave date
    [Column("IstGeloescht")]
    public bool IsDeleted { get; set; }
    [Column("GeloeschtAm")]
    public DateTime? DeletedAt { get; set; }
    [Column("GeloeschtVonId")]
    public string? DeletedById { get; set; }
}
