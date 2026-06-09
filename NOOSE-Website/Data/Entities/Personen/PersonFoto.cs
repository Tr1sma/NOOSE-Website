namespace NOOSE_Website.Data.Entities.Personen;

/// <summary>
/// Ein Foto der Personen-Galerie. Die eigentliche Datei liegt geschützt außerhalb von wwwroot;
/// hier stehen nur die Metadaten. <see cref="DateinameGespeichert"/> wird serverseitig vergeben
/// und ist nie vom Nutzer beeinflussbar (Schutz vor Path-Traversal).
/// </summary>
public class PersonFoto
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PersonId { get; set; } = string.Empty;
    public Person? Person { get; set; }
    public string DateinameGespeichert { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long GroesseBytes { get; set; }
    public DateTime ErstelltAm { get; set; }
    public string? ErstelltVonId { get; set; }
}
