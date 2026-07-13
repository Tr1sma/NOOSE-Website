namespace NOOSE_Website.Models.Activities;

/// <summary>Create/edit input for a personal agent activity.</summary>
public class AgentActivityInput
{
    public string Title { get; set; } = string.Empty;

    /// <summary>Free-text activity kind.</summary>
    public string? Kind { get; set; }

    /// <summary>Local activity date; the service converts to UTC.</summary>
    public DateTime ActivityDate { get; set; } = DateTime.Now;

    /// <summary>Editor HTML body (sanitized in the service).</summary>
    public string ContentHtml { get; set; } = string.Empty;

    /// <summary>Linked factions / person groups.</summary>
    public List<AgentActivityOrgRef> OrgLinks { get; set; } = new();
}
