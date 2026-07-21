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
}

/// <summary>Display labels and chip colors.</summary>
public static class AgentNoteKindDisplay
{
    public static readonly IReadOnlyList<AgentNoteKind> All = new[]
    {
        AgentNoteKind.Commendation,
        AgentNoteKind.Disciplinary,
        AgentNoteKind.Specialization,
        AgentNoteKind.Department,
        AgentNoteKind.Training,
        AgentNoteKind.Information,
    };

    public static string Name(AgentNoteKind kind) => kind switch
    {
        AgentNoteKind.Commendation => "Belobigung",
        AgentNoteKind.Disciplinary => "Negativer Vermerk",
        AgentNoteKind.Specialization => "Spezialisierung",
        AgentNoteKind.Department => "Abteilung",
        AgentNoteKind.Training => "Ausbildung",
        AgentNoteKind.Information => "Information",
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
        _ => Color.Default,
    };
}
