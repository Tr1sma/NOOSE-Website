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

    /// <summary>Activity title.</summary>
    [Column("Titel")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Activity kind/category.</summary>
    [Column("Art")]
    public string? Kind { get; set; }

    /// <summary>Action timestamp (UTC).</summary>
    [Column("Zeitpunkt")]
    public DateTime Timestamp { get; set; }

    /// <summary>Activity description.</summary>
    [Column("Beschreibung")]
    public string? Description { get; set; }

    /// <summary>Action location.</summary>
    [Column("Ort")]
    public string? Location { get; set; }

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
