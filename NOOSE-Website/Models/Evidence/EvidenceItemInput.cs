namespace NOOSE_Website.Models.Evidence;

/// <summary>Form model for creating/editing an evidence catalog item.</summary>
public class EvidenceItemInput
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
