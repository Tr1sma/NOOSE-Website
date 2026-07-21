using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Meetings;

/// <summary>Frozen roster row; not soft-deleted, a historical snapshot must not get holes.</summary>
[Table("BesprechungAnwesenheiten")]
public class MeetingAttendance : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("BesprechungId")]
    public string MeetingId { get; set; } = string.Empty;
    public Meeting? Meeting { get; set; }

    public string AgentId { get; set; } = string.Empty;
    public Agent? Agent { get; set; }

    /// <summary>Codename as of closing; the roster must stay readable after a rename.</summary>
    [Column("AgentCodename")]
    public string? AgentCodename { get; set; }

    public MeetingAttendanceStatus Status { get; set; } = MeetingAttendanceStatus.Open;

    [Column("Herkunft")]
    public MeetingAbsenceOrigin Origin { get; set; } = MeetingAbsenceOrigin.None;

    /// <summary>Reason copied from the absence or sign-off at closing time.</summary>
    [Column("Grund")]
    public string? Reason { get; set; }

    [Column("ErfasstAm")]
    public DateTime? MarkedAt { get; set; }
    [Column("ErfasstVonId")]
    public string? MarkedById { get; set; }

    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }
}
