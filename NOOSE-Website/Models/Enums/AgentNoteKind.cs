using MudBlazor;

namespace NOOSE_Website.Models.Enums;

/// <summary>Personnel file entry type.</summary>
public enum AgentNoteKind
{
    Commendation = 0,
    Disciplinary = 1,
    Specialization = 2,
    Department = 3,
    Training = 4,
    Information = 5,
    Termination = 6,
}

/// <summary>Display labels and chip colors.</summary>
public static class AgentNoteKindDisplay
{
    /// <summary>Kinds an operator may pick when writing an entry by hand.</summary>
    public static readonly IReadOnlyList<AgentNoteKind> Creatable = new[]
    {
        AgentNoteKind.Commendation,
        AgentNoteKind.Disciplinary,
        AgentNoteKind.Specialization,
        AgentNoteKind.Department,
        AgentNoteKind.Training,
        AgentNoteKind.Information,
    };

    /// <summary>Every kind, including the ones only the system writes; use for filters.</summary>
    public static readonly IReadOnlyList<AgentNoteKind> All = new[]
    {
        AgentNoteKind.Commendation,
        AgentNoteKind.Disciplinary,
        AgentNoteKind.Specialization,
        AgentNoteKind.Department,
        AgentNoteKind.Training,
        AgentNoteKind.Information,
        AgentNoteKind.Termination,
    };

    public static string Name(AgentNoteKind kind) => kind switch
    {
        AgentNoteKind.Commendation => "Belobigung",
        AgentNoteKind.Disciplinary => "Negativer Vermerk",
        AgentNoteKind.Specialization => "Spezialisierung",
        AgentNoteKind.Department => "Abteilung",
        AgentNoteKind.Training => "Ausbildung",
        AgentNoteKind.Information => "Information",
        AgentNoteKind.Termination => "Kündigung",
        _ => "—",
    };

    public static Color ChipColor(AgentNoteKind kind) => kind switch
    {
        AgentNoteKind.Commendation => Color.Success,
        AgentNoteKind.Disciplinary => Color.Error,
        AgentNoteKind.Specialization => Color.Primary,
        AgentNoteKind.Department => Color.Tertiary,
        AgentNoteKind.Training => Color.Warning,
        AgentNoteKind.Information => Color.Info,
        AgentNoteKind.Termination => Color.Error,
        _ => Color.Default,
    };
}
