using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.People;

/// <summary>
/// Ein Eintrag im Einstufungs-Verlauf einer Akte (append-only Historie). Polymorph über
/// <see cref="EntitaetTyp"/> + <see cref="EntitaetId"/> (wie Audit-/Zugriffs-Log und die übrigen
/// Querschnitts-Assoziationen) – gemeinsam genutzt von Person, Fraktion und Personengruppe.
/// </summary>
[Table("EinstufungVerlauf")]
public class ClassificationHistory
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>CLR-Typname der Akte (z. B. <c>nameof(Person)</c>, <c>nameof(Fraktion)</c>).</summary>
    [Column("EntitaetTyp")]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Id der zugehörigen Akte.</summary>
    [Column("EntitaetId")]
    public string EntityId { get; set; } = string.Empty;

    [Column("Wert")]
    public Classification Value { get; set; }
    [Column("Begruendung")]
    public string? Justification { get; set; }
    [Column("Zeitpunkt")]
    public DateTime Timestamp { get; set; }
    public string? AgentId { get; set; }
    public string? AgentName { get; set; }

    /// <summary>Platzhalter für den späteren Antrags-Bezug (Phase 5).</summary>
    [Column("AntragId")]
    public string? RequestId { get; set; }
}
