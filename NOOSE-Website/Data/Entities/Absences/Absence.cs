using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Absences;

/// <summary>Self-service sign-off over whole days; leadership acknowledges but never approves.</summary>
[Table("Abmeldungen")]
public class Absence : IAuditable, ISoftDelete
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string AgentId { get; set; } = string.Empty;
    public Agent? Agent { get; set; }

    /// <summary>First day off; a calendar day, never an instant.</summary>
    [Column("VonDatum")]
    public DateOnly FromDate { get; set; }

    /// <summary>Last day off, inclusive.</summary>
    [Column("BisDatum")]
    public DateOnly ToDate { get; set; }

    /// <summary>Denormalised span; the service recomputes it on every write.</summary>
    [Column("Tage")]
    public int Days { get; set; }

    /// <summary>Column is not "Kategorie": AuditDisplay formats enums by German column name and Termine already owns it.</summary>
    [Column("Abmeldegrund")]
    public AbsenceCategory Category { get; set; } = AbsenceCategory.Vacation;

    /// <summary>Free text; leadership only.</summary>
    [Column("Grund")]
    public string? Reason { get; set; }

    /// <summary>Null = leadership has not acknowledged yet.</summary>
    [Column("KenntnisGenommenAm")]
    public DateTime? AcknowledgedAt { get; set; }

    [Column("KenntnisGenommenVonId")]
    public string? AcknowledgedById { get; set; }

    [Column("KenntnisGenommenVonName")]
    public string? AcknowledgedByName { get; set; }

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
