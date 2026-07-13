using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Activities;

/// <summary>A personal activity an agent logs about themselves; visible to all, editable only by owner or leadership.</summary>
[Table("Aktivitaeten")]
public class AgentActivity : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("Titel")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Free-text activity kind (autocompleted from existing values).</summary>
    [Column("Art")]
    public string? Kind { get; set; }

    /// <summary>When the activity happened (stored UTC).</summary>
    [Column("Datum")]
    public DateTime ActivityDate { get; set; }

    /// <summary>Server-side sanitized HTML content.</summary>
    [Column("InhaltHtml")]
    public string ContentHtml { get; set; } = string.Empty;

    /// <summary>Linked factions / person groups this activity references.</summary>
    public List<AgentActivityLink> Links { get; set; } = new();

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
