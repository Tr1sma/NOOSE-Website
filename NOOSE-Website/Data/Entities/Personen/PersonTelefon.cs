namespace NOOSE_Website.Data.Entities.Personen;

/// <summary>Eine Telefonnummer einer Person – Steckbrief-Feld und Basis der Dublettensuche.</summary>
public class PersonTelefon
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PersonId { get; set; } = string.Empty;
    public Person? Person { get; set; }
    public string Nummer { get; set; } = string.Empty;
    public string? Bezeichnung { get; set; }
}
