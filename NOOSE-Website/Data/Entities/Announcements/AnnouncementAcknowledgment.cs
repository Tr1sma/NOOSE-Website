using NOOSE_Website.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Announcements;

/// <summary>
/// Quittierung (Lesebestätigung) eines Agenten zu einer Ankündigung – Phase 6. Join-Entity mit
/// <see cref="IAuditable"/> (kein Soft-Delete – Muster <see cref="Aufgaben.AufgabeZuweisung"/>). Die Zeilen werden
/// beim Anlegen einer Ankündigung mit <c>QuittierungVerlangt</c> als Empfänger-Snapshot erzeugt; <see cref="QuittiertAm"/>
/// bleibt null, bis der Agent „Kenntnis nimmt". FK auf den <see cref="Agent"/> ist <c>Restrict</c>, FK auf die
/// Ankündigung ist Cascade.
/// </summary>
[Table("AnkuendigungQuittierungen")]
public class AnnouncementAcknowledgment : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("AnkuendigungId")]
    public string AnnouncementId { get; set; } = string.Empty;
    public Announcement? Announcement { get; set; }

    public string AgentId { get; set; } = string.Empty;
    public Agent? Agent { get; set; }

    /// <summary>Zeitpunkt der Kenntnisnahme; null = noch offen.</summary>
    [Column("QuittiertAm")]
    public DateTime? AcknowledgedAt { get; set; }

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
