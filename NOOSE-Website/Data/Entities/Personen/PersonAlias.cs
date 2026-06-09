namespace NOOSE_Website.Data.Entities.Personen;

/// <summary>Ein Alias/Spitzname einer Person (Steckbrief-Kind, hart gelöscht beim Entfernen).</summary>
public class PersonAlias
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PersonId { get; set; } = string.Empty;
    public Person? Person { get; set; }
    public string Aliasname { get; set; } = string.Empty;
}
