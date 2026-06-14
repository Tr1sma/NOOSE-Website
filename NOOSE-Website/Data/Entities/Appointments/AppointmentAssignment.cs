using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Appointments;

/// <summary>
/// Teilnehmer eines Termins (Zuteilung an einen NOOSE-Agent) – Phase 8 (Block C). Join-Entity mit
/// <see cref="IAuditable"/>. FK auf den <see cref="Agent"/> (Identity-Tabelle) ist <c>Restrict</c>;
/// FK auf den Termin ist Cascade. Vorlage: <see cref="Aufgaben.AufgabeZuweisung"/>.
/// </summary>
[Table("TerminZuweisungen")]
public class AppointmentAssignment : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("TerminId")]
    public string AppointmentId { get; set; } = string.Empty;
    public Appointment? Appointment { get; set; }

    public string AgentId { get; set; } = string.Empty;
    public Agent? Agent { get; set; }

    // ---- IAuditable ----
    [Column("ErstelltAm")]
    public DateTime CreatedAt { get; set; }
    [Column("ErstelltVonId")]
    public string? CreatedById { get; set; }
    [Column("GeaendertAm")]
    public DateTime? ModifiedAt { get; set; }
    [Column("GeaendertVonId")]
    public string? ModifiedById { get; set; }
}
