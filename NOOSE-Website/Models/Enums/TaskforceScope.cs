namespace NOOSE_Website.Models.Enums;

/// <summary>Taskforce operational scope.</summary>
public enum TaskforceScope
{
    /// <summary>NOOSE-internal only.</summary>
    InternalAgency = 0,
    /// <summary>Cross-agency, multi-department.</summary>
    CrossAgency = 1,
}

/// <summary>Display labels.</summary>
public static class TaskforceScopeDisplay
{
    public static string Name(TaskforceScope area) => area switch
    {
        TaskforceScope.InternalAgency => "Innerbehördlich",
        TaskforceScope.CrossAgency => "Überbehördlich",
        _ => "—",
    };

    public static readonly IReadOnlyList<TaskforceScope> All = new[]
    {
        TaskforceScope.InternalAgency,
        TaskforceScope.CrossAgency,
    };
}
