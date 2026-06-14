using System.ComponentModel.DataAnnotations.Schema;
namespace NOOSE_Website.Data.Entities.Fraktionen;

/// <summary>
/// Ein Foto der Fraktions-Galerie. Die eigentliche Datei liegt geschützt außerhalb von wwwroot;
/// hier stehen nur die Metadaten. <see cref="DateinameGespeichert"/> wird serverseitig vergeben
/// und ist nie vom Nutzer beeinflussbar (Schutz vor Path-Traversal). Genau eines der Fotos einer
/// Fraktion kann als <see cref="IstTitelbild"/> markiert sein (im Dienst transaktional erzwungen).
/// </summary>
[Table("FraktionFotos")]
public class FraktionFoto
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    [Column("FraktionId")]
    public string FraktionId { get; set; } = string.Empty;
    public Fraktion? Fraktion { get; set; }
    [Column("DateinameGespeichert")]
    public string DateinameGespeichert { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    [Column("GroesseBytes")]
    public long GroesseBytes { get; set; }

    /// <summary>Als Titelbild der Fraktion markiert (ersetzt den Farb-Banner der Steckkarte; höchstens eines je Fraktion).</summary>
    [Column("IstTitelbild")]
    public bool IstTitelbild { get; set; }

    [Column("ErstelltAm")]
    public DateTime ErstelltAm { get; set; }
    [Column("ErstelltVonId")]
    public string? ErstelltVonId { get; set; }
}
