using NOOSE_Website.Data.Entities.Personnel;

namespace NOOSE_Website.Models.Personnel;

/// <summary>Create/edit training module input.</summary>
public class ModuleInput
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public int Sorting { get; set; }
}

/// <summary>A training module plus the viewed agent's completion status.</summary>
public record AgentModuleStatus(
    TrainingModule Module,
    string? CompletionId,
    DateTime? CompletedAt,
    string? CompleterName,
    string? Note)
{
    public bool IsCompleted => CompletionId is not null;
}
