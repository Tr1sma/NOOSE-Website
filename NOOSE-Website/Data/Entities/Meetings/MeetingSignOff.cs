using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Meetings;

/// <summary>Sign-off for one meeting only; separate from attendance because it exists before anyone ticks.</summary>
[Table("BesprechungAbmeldungen")]
public class MeetingSignOff : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("BesprechungId")]
    public string MeetingId { get; set; } = string.Empty;
    public Meeting? Meeting { get; set; }

    public string AgentId { get; set; } = string.Empty;
    public Agent? Agent { get; set; }

    [Column("Grund")]
    public string? Reason { get; set; }

    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }
}
