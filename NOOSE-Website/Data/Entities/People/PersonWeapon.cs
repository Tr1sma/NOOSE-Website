using System.ComponentModel.DataAnnotations.Schema;
namespace NOOSE_Website.Data.Entities.People;

/// <summary>A weapon associated with a person.</summary>
[Table("PersonWaffen")]
public class PersonWeapon
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PersonId { get; set; } = string.Empty;
    public Person? Person { get; set; }
    public string Text { get; set; } = string.Empty;
}
