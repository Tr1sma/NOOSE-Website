using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Announcements;

/// <summary>An agent's acknowledgment of an announcement, created as a recipient snapshot. FK to Agent is Restrict; FK to Announcement is Cascade.</summary>
[Table("AnkuendigungQuittierungen")]
public class AnnouncementAcknowledgment : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("AnkuendigungId")]
    public string AnnouncementId { get; set; } = string.Empty;
    public Announcement? Announcement { get; set; }

    public string AgentId { get; set; } = string.Empty;
    public Agent? Agent { get; set; }

    /// <summary>Acknowledgment time; null = still pending.</summary>
    [Column("QuittiertAm")]
    public DateTime? AcknowledgedAt { get; set; }

    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }
}
