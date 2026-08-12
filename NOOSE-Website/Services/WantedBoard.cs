using NOOSE_Website.Data.Entities.People;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Who shows on the wanted board — the one rule shared by the Fahndung page and the assistant.</summary>
/// <remarks>Membership is the manual flag or a threat score at/above the configured hazard threshold. It lived only
/// in the Razor panel, so NOOSEI could not answer "who is on the wanted list"; kept here the page and the tools
/// cannot drift.</remarks>
public static class WantedBoard
{
    /// <summary>True when the person appears on the wanted board at the given threshold.</summary>
    public static bool IsOnBoard(Person person, HazardLevel threshold)
        => IsOnBoard(person.IsWanted, person.ThreatScore, threshold);

    /// <summary>The board rule on the two fields that decide it, for callers holding a projection, not the entity.</summary>
    public static bool IsOnBoard(bool isWanted, int? threatScore, HazardLevel threshold)
        => isWanted || HazardLevelLogic.From(threatScore) >= threshold;

    /// <summary>Why they are on it — the manual note beats the score, matching the page's own ordering.</summary>
    public static string Reason(Person person, HazardLevel threshold)
        => person.IsWanted
            ? "manuell ausgeschrieben"
            : "ab Gefahrenstufe " + HazardLevelLogic.Name(HazardLevelLogic.From(person.ThreatScore));
}
