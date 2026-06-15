using System.ComponentModel.DataAnnotations.Schema;
namespace NOOSE_Website.Data.Entities.People;

/// <summary>Ein Fahrzeug einer Person inkl. optionalem Kennzeichen (Steckbrief-Kind).</summary>
[Table("PersonFahrzeuge")]
public class PersonVehicle
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PersonId { get; set; } = string.Empty;
    public Person? Person { get; set; }
    [Column("Bezeichnung")]
    public string Designation { get; set; } = string.Empty;
    [Column("Kennzeichen")]
    public string? LicensePlate { get; set; }
}
