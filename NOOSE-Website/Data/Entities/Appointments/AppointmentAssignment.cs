using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Appointments;

/// <summary>Appointment participant. FK to Agent is Restrict; FK to Appointment is Cascade.</summary>
[Table("TerminZuweisungen")]
public class AppointmentAssignment : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("TerminId")]
    public string AppointmentId { get; set; } = string.Empty;
    public Appointment? Appointment { get; set; }

    public string AgentId { get; set; } = string.Empty;
    public Agent? Agent { get; set; }

    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }
}
