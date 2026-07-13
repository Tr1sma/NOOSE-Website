using System.ComponentModel.DataAnnotations.Schema;

namespace NOOSE_Website.Data.Entities.Activities;

/// <summary>Links an activity to a faction or person group; polymorphic target, cascades with the activity.</summary>
[Table("AktivitaetVerknuepfungen")]
public class AgentActivityLink
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string AgentActivityId { get; set; } = string.Empty;
    public AgentActivity? AgentActivity { get; set; }

    /// <summary>Linked record type (nameof(Faction) / nameof(PersonGroup)).</summary>
    [Column("EntitaetTyp")]
    public string TargetType { get; set; } = string.Empty;

    [Column("EntitaetId")]
    public string TargetId { get; set; } = string.Empty;
}
