using System.ComponentModel.DataAnnotations.Schema;
namespace NOOSE_Website.Data.Entities.People;

/// <summary>A person's alias/nickname; hard-deleted on removal.</summary>
[Table("PersonAliase")]
public class PersonAlias
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PersonId { get; set; } = string.Empty;
    public Person? Person { get; set; }
    [Column("Aliasname")]
    public string AliasName { get; set; } = string.Empty;
}
