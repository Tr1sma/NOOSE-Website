using NOOSE_Website.Models.Abstractions;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Data.Entities.Taskforces;

/// <summary>
/// Zuteilung eines NOOSE-Agents zu einer Taskforce (Mitglied oder Leitung). Join-Entity mit
/// <see cref="IAuditable"/>. FK auf den <see cref="Agent"/> (Identity-Tabelle) ist <c>Restrict</c>; FK auf
/// die Taskforce ist Cascade. Die <see cref="Rolle"/> bestimmt, ob es sich um ein einfaches Mitglied oder eine
/// Leitung (Chefermittler/CID-Lead/TRU-Lead) handelt; jede Rolle ungleich <see cref="TaskforceRolle.Mitglied"/>
/// berechtigt – wie die Führung – zur Verwaltung der Zuteilungen.
/// </summary>
public class TaskforceAgent : IAuditable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string TaskforceId { get; set; } = string.Empty;
    public Taskforce? Taskforce { get; set; }

    public string AgentId { get; set; } = string.Empty;
    public Agent? Agent { get; set; }

    /// <summary>Rolle des Agents in der Taskforce; Leitung = jede Rolle ungleich <see cref="TaskforceRolle.Mitglied"/>.</summary>
    public TaskforceRolle Rolle { get; set; } = TaskforceRolle.Mitglied;

    // ---- IAuditable ----
    public DateTime ErstelltAm { get; set; }
    public string? ErstelltVonId { get; set; }
    public DateTime? GeaendertAm { get; set; }
    public string? GeaendertVonId { get; set; }
}
