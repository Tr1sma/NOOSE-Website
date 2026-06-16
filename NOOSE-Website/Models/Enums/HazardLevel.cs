namespace NOOSE_Website.Models.Enums;

/// <summary>Faction threat level.</summary>
public enum HazardLevel
{
    No = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4,
}

/// <summary>Score-to-level mapping and labels.</summary>
public static class HazardLevelLogic
{
    /// <summary>Maps score (0-100) to hazard level.</summary>
    public static HazardLevel From(int? score) => score switch
    {
        null or <= 0 => HazardLevel.No,
        < 25 => HazardLevel.Low,
        < 50 => HazardLevel.Medium,
        < 75 => HazardLevel.High,
        _ => HazardLevel.Critical,
    };

    public static string Name(HazardLevel level) => level switch
    {
        HazardLevel.No => "Keine",
        HazardLevel.Low => "Niedrig",
        HazardLevel.Medium => "Mittel",
        HazardLevel.High => "Hoch",
        HazardLevel.Critical => "Kritisch",
        _ => "—",
    };

    public static readonly IReadOnlyList<HazardLevel> All = new[]
    {
        HazardLevel.No,
        HazardLevel.Low,
        HazardLevel.Medium,
        HazardLevel.High,
        HazardLevel.Critical,
    };
}
