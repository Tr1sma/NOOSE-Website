using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Recruiting;

/// <summary>Secret single-use invite link that lets a Discord login become a pending agent (skipping the public application).</summary>
[Table("AgentEinladungen")]
public class AgentInvite : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>URL-safe random token; the link is /invite/{token}.</summary>
    public string Token { get; set; } = string.Empty;

    [Column("Bezeichnung")]
    public string? Label { get; set; }

    [Column("ErstelltVonName")]
    public string? CreatedByName { get; set; }

    [Column("LaeuftAbAm")]
    public DateTime? ExpiresAt { get; set; }

    [Column("EingeloestVonId")]
    public string? UsedByUserId { get; set; }

    [Column("EingeloestAm")]
    public DateTime? UsedAt { get; set; }

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
