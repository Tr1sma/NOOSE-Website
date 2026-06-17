using NOOSE_Website.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.People;

/// <summary>Append-only classification-history entry, polymorphic via EntityType + EntityId across Person, Faction and PersonGroup.</summary>
[Table("EinstufungVerlauf")]
public class ClassificationHistory
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>CLR type name of the record (e.g. nameof(Person)).</summary>
    [Column("EntitaetTyp")]
    public string EntityType { get; set; } = string.Empty;

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

    /// <summary>Placeholder for a future request reference.</summary>
    [Column("AntragId")]
    public string? RequestId { get; set; }
}
