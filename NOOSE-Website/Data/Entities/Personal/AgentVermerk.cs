using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Personal;

/// <summary>
/// Ein Personalakten-Vermerk zu einem Agent (Phase 5e): Belobigung oder Disziplinar-Eintrag. Datierter,
/// auditierter Eintrag mit Autor (Vorlage: <c>Kommentar</c>). Für alle Agenten sichtbar; anlegen/löschen nur
/// durch die Führung. <see cref="AutorName"/> = Codename des Verfassers (denormalisiert).
/// </summary>
[Table("AgentVermerke")]
public class AgentVermerk : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string AgentId { get; set; } = string.Empty;

    [Column("Art")]
    public AgentVermerkArt Art { get; set; }

    public string Text { get; set; } = string.Empty;

    /// <summary>Codename des Verfassers zum Zeitpunkt (denormalisiert).</summary>
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
