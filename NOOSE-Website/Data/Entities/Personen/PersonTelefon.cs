using System.ComponentModel.DataAnnotations.Schema;
namespace NOOSE_Website.Data.Entities.Personen;

/// <summary>Eine Telefonnummer einer Person – Steckbrief-Feld und Basis der Dublettensuche.</summary>
[Table("PersonTelefone")]
public class PersonTelefon
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PersonId { get; set; } = string.Empty;
    public Person? Person { get; set; }
    [Column("Nummer")]
    public string Nummer { get; set; } = string.Empty;
    [Column("Bezeichnung")]
    public string? Bezeichnung { get; set; }
}
