using NOOSE_Website.Data.Entities.Activities;

namespace NOOSE_Website.Models.Activities;

/// <summary>An activity plus its resolved owner name and visible org links, for the detail/edit surfaces.</summary>
public class AgentActivityDetailView
{
    public AgentActivity Activity { get; set; } = default!;
    public string OwnerName { get; set; } = string.Empty;
    public List<AgentActivityOrgRef> Orgs { get; set; } = new();
}
