using System.ComponentModel.DataAnnotations.Schema;
namespace NOOSE_Website.Data.Entities.Personen;

/// <summary>Ein Alias/Spitzname einer Person (Steckbrief-Kind, hart gelöscht beim Entfernen).</summary>
[Table("PersonAliase")]
public class PersonAlias
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PersonId { get; set; } = string.Empty;
    public Person? Person { get; set; }
    [Column("Aliasname")]
    public string Aliasname { get; set; } = string.Empty;
}
