using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Groups;

/// <summary>Join entity for a person's group membership; Restrict FK on Person avoids colliding cascade paths. Soft-delete preserves membership history (DeletedAt = leave date, CreatedAt = join date).</summary>
[Table("PersonengruppeMitglieder")]
public class PersonGroupMember : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("PersonengruppeId")]
    public string PersonGroupId { get; set; } = string.Empty;
    public PersonGroup? PersonGroup { get; set; }

    public string PersonId { get; set; } = string.Empty;
    public Person? Person { get; set; }

    [Column("Rolle")]
    public string? Role { get; set; }

    [Column("IstLeitung")]
    public bool IsLead { get; set; }

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
