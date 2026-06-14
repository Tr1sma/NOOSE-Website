using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Querschnitt;

/// <summary>
/// Ein generischer Kommentar/Vermerk an einer beliebigen Akte (polymorph über <see cref="EntitaetTyp"/>
/// + <see cref="EntitaetId"/>). Voll auditiert und papierkorbfähig. @-Erwähnungen folgen erst in Phase 6.
/// </summary>
[Table("Kommentare")]
public class Kommentar : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("EntitaetTyp")]
    public string EntitaetTyp { get; set; } = string.Empty;
    [Column("EntitaetId")]
    public string EntitaetId { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    /// <summary>Codename des Autors zum Zeitpunkt der Erstellung (denormalisiert, wie EinstufungVerlauf.AgentName).</summary>
    [Column("AutorName")]
    public string? AutorName { get; set; }

    // ---- IAuditable ----
    [Column("ErstelltAm")]
    public DateTime ErstelltAm { get; set; }
    [Column("ErstelltVonId")]
    public string? ErstelltVonId { get; set; }
    [Column("GeaendertAm")]
    public DateTime? GeaendertAm { get; set; }
    [Column("GeaendertVonId")]
    public string? GeaendertVonId { get; set; }

    // ---- ISoftDelete ----
    [Column("IstGeloescht")]
    public bool IstGeloescht { get; set; }
    [Column("GeloeschtAm")]
    public DateTime? GeloeschtAm { get; set; }
    [Column("GeloeschtVonId")]
    public string? GeloeschtVonId { get; set; }
}
