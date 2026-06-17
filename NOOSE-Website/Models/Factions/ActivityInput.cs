namespace NOOSE_Website.Models.Factions;

/// <summary>Input model for a faction activity (timeline entry); Timestamp captured local, stored UTC.</summary>
public class ActivityInput
{
    public string Title { get; set; } = string.Empty;

    public string? Kind { get; set; }

    public DateTime Timestamp { get; set; }

    public string? Description { get; set; }

    public string? Location { get; set; }
}
