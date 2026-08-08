using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Informants;

/// <summary>A confidential informant (V-Person). Informants have no codename — they are identified by their real name,
/// either free-text or derived from a linked person record.</summary>
[Table("Informanten")]
public class Informant : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("Aktenzeichen")]
    public string CaseNumber { get; set; } = string.Empty;

    /// <summary>Free-text real name; stays empty while <see cref="PersonId"/> is set (the record is the name source).</summary>
    [Column("Klarname")]
    public string? RealName { get; set; }

    /// <summary>Linked person record, at most one informant per person.</summary>
    [Column("PersonId")]
    public string? PersonId { get; set; }

    /// <summary>Faction the informant reports on; several informants may share one faction.</summary>
    [Column("FraktionId")]
    public string? FactionId { get; set; }

    [Column("Beschreibung")]
    public string? Description { get; set; }

    [Column("Kontakt")]
    public string? ContactInfo { get; set; }

    [Column("Notizen")]
    public string? Notes { get; set; }

    [Column("Zuverlaessigkeit")]
    public InformantReliability Reliability { get; set; } = InformantReliability.C;

    [Column("Status")]
    public InformantStatus Status { get; set; } = InformantStatus.Active;

    /// <summary>Assigned handler (Führungsagent). Restrict — never cascade off the Agent table.</summary>
    public string HandlerId { get; set; } = string.Empty;

    public List<InformantMeeting> Meetings { get; set; } = new();

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
