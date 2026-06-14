using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Taskforces;

/// <summary>
/// Zuteilung eines NOOSE-Agents zu einer Taskforce (Mitglied oder Leitung). Join-Entity mit
/// <see cref="IAuditable"/>. FK auf den <see cref="Agent"/> (Identity-Tabelle) ist <c>Restrict</c>; FK auf
/// die Taskforce ist Cascade. Die <see cref="Rolle"/> bestimmt, ob es sich um ein einfaches Mitglied oder eine
/// Leitung (Chefermittler/CID-Lead/TRU-Lead) handelt; jede Rolle ungleich <see cref="TaskforceRolle.Mitglied"/>
/// berechtigt – wie die Führung – zur Verwaltung der Zuteilungen.
/// </summary>
[Table("TaskforceAgenten")]
public class TaskforceAgent : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string TaskforceId { get; set; } = string.Empty;
    public Taskforce? Taskforce { get; set; }

    public string AgentId { get; set; } = string.Empty;
    public Agent? Agent { get; set; }

    /// <summary>Rolle des Agents in der Taskforce; Leitung = jede Rolle ungleich <see cref="TaskforceRolle.Mitglied"/>.</summary>
    [Column("Rolle")]
    public TaskforceRole Role { get; set; } = TaskforceRole.Member;

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
