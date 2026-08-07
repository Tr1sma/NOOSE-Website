namespace NOOSE_Website.Models.Evidence;

/// <summary>Form model for creating/editing an evidence catalog item.</summary>
public class EvidenceItemInput
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Grouping label; a new value is learned into the suggestion catalog on save.</summary>
    public string? Category { get; set; }
}
