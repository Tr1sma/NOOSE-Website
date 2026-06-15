using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Personnel;

/// <summary>
/// Ein Personalakten-Vermerk zu einem Agent (Phase 5e): Belobigung oder Disziplinar-Eintrag. Datierter,
/// auditierter Eintrag mit Autor (Vorlage: <c>Kommentar</c>). Für alle Agenten sichtbar; anlegen/löschen nur
/// durch die Führung. <see cref="AutorName"/> = Codename des Verfassers (denormalisiert).
/// </summary>
[Table("AgentVermerke")]
public class AgentNote : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string AgentId { get; set; } = string.Empty;

    [Column("Art")]
    public AgentNoteKind Kind { get; set; }

    public string Text { get; set; } = string.Empty;

    /// <summary>Codename des Verfassers zum Zeitpunkt (denormalisiert).</summary>
    [Column("AutorName")]
    public string? AuthorName { get; set; }

    // ---- IAuditable ----
    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }

    // ---- ISoftDelete ----
    [Column("IstGeloescht")]
    public bool IsDeleted { get; set; }
    [Column("GeloeschtAm")]
    public DateTime? DeletedAt { get; set; }
    [Column("GeloeschtVonId")]
    public string? DeletedById { get; set; }
}
