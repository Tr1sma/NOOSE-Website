using System.ComponentModel.DataAnnotations.Schema;
namespace NOOSE_Website.Data.Entities.Personen;

/// <summary>Eine der Person zugeordnete Waffe (Steckbrief-Kind).</summary>
[Table("PersonWaffen")]
public class PersonWaffe
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PersonId { get; set; } = string.Empty;
    public Person? Person { get; set; }
    public string Text { get; set; } = string.Empty;
}
