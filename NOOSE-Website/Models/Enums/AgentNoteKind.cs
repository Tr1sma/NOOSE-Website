namespace NOOSE_Website.Models.Enums;

/// <summary>Art eines Personalakten-Vermerks (Phase 5e): positive Belobigung oder Disziplinar-Eintrag.</summary>
public enum AgentNoteKind
{
    Commendation = 0,
    Disciplinary = 1,
}

/// <summary>Anzeigetexte für die Vermerk-Art (UI-frei).</summary>
public static class AgentNoteKindDisplay
{
    public static string Name(AgentNoteKind kind) => kind switch
    {
        AgentNoteKind.Commendation => "Belobigung",
        AgentNoteKind.Disciplinary => "Disziplinarisch",
        _ => "—",
    };
}
