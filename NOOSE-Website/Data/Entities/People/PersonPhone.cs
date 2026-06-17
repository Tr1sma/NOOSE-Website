using System.ComponentModel.DataAnnotations.Schema;
namespace NOOSE_Website.Data.Entities.People;

/// <summary>A person's phone number; basis of duplicate detection.</summary>
[Table("PersonTelefone")]
public class PersonPhone
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PersonId { get; set; } = string.Empty;
    public Person? Person { get; set; }
    [Column("Nummer")]
    public string Number { get; set; } = string.Empty;
    [Column("Bezeichnung")]
    public string? Designation { get; set; }
}
