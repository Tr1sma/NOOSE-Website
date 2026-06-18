using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Factions;

/// <summary>Faction activity entry; fully audited and soft-deletable.</summary>
[Table("FraktionAktivitaeten")]
public class FactionActivity : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("FraktionId")]
    public string FactionId { get; set; } = string.Empty;
    public Faction? Faction { get; set; }

    [Column("Titel")]
    public string Title { get; set; } = string.Empty;

    [Column("Art")]
    public string? Kind { get; set; }

    /// <summary>Action timestamp (UTC).</summary>
    [Column("Zeitpunkt")]
    public DateTime Timestamp { get; set; }

    [Column("Beschreibung")]
    public string? Description { get; set; }

    [Column("Ort")]
    public string? Location { get; set; }

    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }

    [Column("IstGeloescht")]
    public bool IsDeleted { get; set; }
    [Column("GeloeschtAm")]
    public DateTime? DeletedAt { get; set; }
    [Column("GeloeschtVonId")]
    public string? DeletedById { get; set; }
}
