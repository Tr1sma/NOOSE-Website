namespace NOOSE_Website.Models.Activities;

/// <summary>Projected activity row for lists, the faction/group tab, and trash.</summary>
public class AgentActivityListItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Kind { get; set; }
    public DateTime ActivityDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    /// <summary>Owning agent id (creator) and display codename.</summary>
    public string? OwnerId { get; set; }
    public string OwnerName { get; set; } = string.Empty;

    /// <summary>Visible linked factions / person groups.</summary>
    public List<AgentActivityOrgRef> Orgs { get; set; } = new();

    /// <summary>Short plain-text of the content for client-side quick filtering.</summary>
    public string ContentPlain { get; set; } = string.Empty;
}
