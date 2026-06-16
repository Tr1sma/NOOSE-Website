namespace NOOSE_Website.Models.Enums;

/// <summary>Task priority level.</summary>
public enum JobPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
}

/// <summary>Display labels.</summary>
public static class JobPriorityDisplay
{
    public static string Name(JobPriority priority) => priority switch
    {
        JobPriority.Low => "Niedrig",
        JobPriority.Normal => "Normal",
        JobPriority.High => "Hoch",
        _ => "—",
    };

    public static readonly IReadOnlyList<JobPriority> All = new[]
    {
        JobPriority.Low,
        JobPriority.Normal,
        JobPriority.High,
    };
}
