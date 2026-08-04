using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Informants;

/// <summary>A confidential informant (V-Person). Public face is the codename; the real identity lives in a separate,
/// deliberately un-audited table so it can never leak via the change log.</summary>
[Table("Informanten")]
public class Informant : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("Aktenzeichen")]
    public string CaseNumber { get; set; } = string.Empty;

    [Column("Deckname")]
    public string Codename { get; set; } = string.Empty;

    [Column("Beschreibung")]
    public string? Description { get; set; }

    [Column("Zuverlaessigkeit")]
    public InformantReliability Reliability { get; set; } = InformantReliability.C;

    [Column("Status")]
    public InformantStatus Status { get; set; } = InformantStatus.Active;

    /// <summary>Assigned handler (Führungsagent). Restrict — never cascade off the Agent table.</summary>
    public string HandlerId { get; set; } = string.Empty;

    public InformantIdentity? Identity { get; set; }
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
