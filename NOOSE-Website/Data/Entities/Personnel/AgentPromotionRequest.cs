using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Personnel;

/// <summary>
/// Ein Beförderungsantrag für einen Agent (Phase 5e). Die Führung (Supervisory+) schlägt einen
/// <see cref="ZielDienstgrad"/> vor; Deputy Director+/Admin entscheidet. Bei Genehmigung wird der Rang des
/// Agents gesetzt und im Dienstgrad-Verlauf protokolliert (siehe <c>AgentVerwaltungService</c>).
/// </summary>
[Table("AgentBefoerderungsantraege")]
public class AgentPromotionRequest : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Agent, der befördert werden soll.</summary>
    public string AgentId { get; set; } = string.Empty;

    [Column("ZielDienstgrad")]
    public Rank TargetRank { get; set; }

    [Column("Begruendung")]
    public string? Justification { get; set; }

    public PromotionStatus Status { get; set; } = PromotionStatus.Requested;

    /// <summary>Codename des Antragstellers (denormalisiert).</summary>
    [Column("AntragstellerName")]
    public string? RequesterName { get; set; }

    /// <summary>Codename des Entscheiders (denormalisiert), null solange offen.</summary>
    [Column("EntscheiderName")]
    public string? DeciderName { get; set; }

    [Column("EntschiedenAm")]
    public DateTime? DecidedAt { get; set; }

    [Column("Entscheidungsnotiz")]
    public string? DecisionNote { get; set; }

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
