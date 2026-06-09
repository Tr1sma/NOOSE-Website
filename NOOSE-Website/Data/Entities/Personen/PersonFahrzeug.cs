namespace NOOSE_Website.Data.Entities.Personen;

/// <summary>Ein Fahrzeug einer Person inkl. optionalem Kennzeichen (Steckbrief-Kind).</summary>
public class PersonFahrzeug
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PersonId { get; set; } = string.Empty;
    public Person? Person { get; set; }
    public string Bezeichnung { get; set; } = string.Empty;
    public string? Kennzeichen { get; set; }
}
