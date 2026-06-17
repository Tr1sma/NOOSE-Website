using System.ComponentModel.DataAnnotations.Schema;
namespace NOOSE_Website.Data.Entities.Common;

/// <summary>Links a tag to any record (polymorphic by type + id); hard-deleted on untag.</summary>
[Table("TagZuordnungen")]
public class TagMapping
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string TagId { get; set; } = string.Empty;
    public Tag? Tag { get; set; }

    [Column("EntitaetTyp")]
    public string EntityType { get; set; } = string.Empty;
    [Column("EntitaetId")]
    public string EntityId { get; set; } = string.Empty;
}
