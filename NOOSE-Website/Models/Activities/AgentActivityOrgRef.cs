namespace NOOSE_Website.Models.Activities;

/// <summary>A faction or person-group reference linked to an activity, with a resolved display name.</summary>
public class AgentActivityOrgRef
{
    /// <summary>Linked record type (nameof(Faction) / nameof(PersonGroup)).</summary>
    public string TargetType { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}
