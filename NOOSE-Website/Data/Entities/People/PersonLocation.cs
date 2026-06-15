using System.ComponentModel.DataAnnotations.Schema;
namespace NOOSE_Website.Data.Entities.People;

/// <summary>Ein bekannter Ort/Aufenthaltsort einer Person (Steckbrief-Kind).</summary>
[Table("PersonOrte")]
public class PersonLocation
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PersonId { get; set; } = string.Empty;
    public Person? Person { get; set; }
    public string Text { get; set; } = string.Empty;
    [Column("Notiz")]
    public string? Note { get; set; }
}
