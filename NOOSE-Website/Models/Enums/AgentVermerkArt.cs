namespace NOOSE_Website.Models.Enums;

/// <summary>Art eines Personalakten-Vermerks (Phase 5e): positive Belobigung oder Disziplinar-Eintrag.</summary>
public enum AgentVermerkArt
{
    Belobigung = 0,
    Disziplinarisch = 1,
}

/// <summary>Anzeigetexte für die Vermerk-Art (UI-frei).</summary>
public static class AgentVermerkArtAnzeige
{
    public static string Name(AgentVermerkArt art) => art switch
    {
        AgentVermerkArt.Belobigung => "Belobigung",
        AgentVermerkArt.Disziplinarisch => "Disziplinarisch",
        _ => "—",
    };
}
