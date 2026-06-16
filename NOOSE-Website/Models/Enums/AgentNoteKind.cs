namespace NOOSE_Website.Models.Enums;

/// <summary>Personnel file note type.</summary>
public enum AgentNoteKind
{
    Commendation = 0,
    Disciplinary = 1,
}

/// <summary>Display labels.</summary>
public static class AgentNoteKindDisplay
{
    public static string Name(AgentNoteKind kind) => kind switch
    {
        AgentNoteKind.Commendation => "Belobigung",
        AgentNoteKind.Disciplinary => "Disziplinarisch",
        _ => "—",
    };
}
