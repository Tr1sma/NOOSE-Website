using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Evidence;

/// <summary>A deposit or withdrawal booked against the evidence room; carries one or more item positions.</summary>
[Table("AsservatEintraege")]
public class EvidenceEntry : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("Aktenzeichen")]
    public string CaseNumber { get; set; } = string.Empty;

    [Column("Art")]
    public EvidenceEntryType Type { get; set; }

    /// <summary>Owner record type: "NOOSE" / nameof(Agent) / nameof(Person). Loose link, no FK.</summary>
    [Column("BesitzerTyp")]
    public string OwnerType { get; set; } = string.Empty;

    /// <summary>Owner id; null for the NOOSE sentinel.</summary>
    [Column("BesitzerId")]
    public string? OwnerId { get; set; }

    /// <summary>Agent who booked the entry.</summary>
    [Column("BearbeiterId")]
    public string HandlerAgentId { get; set; } = string.Empty;
    public Agent? HandlerAgent { get; set; }

    /// <summary>When the deposit/withdrawal happened (stored UTC).</summary>
    [Column("Zeitpunkt")]
    public DateTime Timestamp { get; set; }

    [Column("Notiz")]
    public string? Notes { get; set; }

    /// <summary>Item positions of this entry.</summary>
    public List<EvidenceEntryLine> Lines { get; set; } = new();

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
