using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Notifications;

/// <summary>In-app notification for one recipient agent; uses a direct Href rather than a polymorphic target (targets are heterogeneous).</summary>
[Table("Benachrichtigungen")]
public class Notification : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("EmpfaengerId")]
    public string RecipientId { get; set; } = string.Empty;

    [Column("Typ")]
    public NotificationType Type { get; set; }

    /// <summary>Denormalised display text (no sensitive case/classified names).</summary>
    [Column("Titel")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Click target link; null = not clickable.</summary>
    public string? Href { get; set; }

    /// <summary>Read timestamp; null = unread (counts toward the badge).</summary>
    [Column("GelesenAm")]
    public DateTime? ReadAt { get; set; }

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
