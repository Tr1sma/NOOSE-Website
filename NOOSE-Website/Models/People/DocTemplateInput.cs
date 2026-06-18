using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.People;

/// <summary>Form model for creating/editing a doc template.</summary>
public class DocTemplateInput
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int Sorting { get; set; }

    public string? DefaultReason { get; set; }
    public string? DefaultFaction { get; set; }
    public string? DefaultReceivedInformation { get; set; }
    public bool DefaultTruthSerum { get; set; }
    public MeasureOutcome DefaultOutcome { get; set; } = MeasureOutcome.RunningStill;
}
