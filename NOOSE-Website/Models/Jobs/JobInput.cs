using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Jobs;

/// <summary>Create/edit job/to-do input.</summary>
public class JobInput
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Open;
    public JobPriority Priority { get; set; } = JobPriority.Normal;
    public DateTime? DueDate { get; set; }

    /// <summary>Restricted: only assignees, creator and supervision see the job.</summary>
    public bool IsRestricted { get; set; }
}
