namespace NOOSE_Website.Models.Enums;

/// <summary>Leaderboard aggregation window.</summary>
public enum GamificationPeriod
{
    AllTime = 0,
    Month = 1,
    Week = 2,
}

/// <summary>Display labels.</summary>
public static class GamificationPeriodDisplay
{
    public static string Name(GamificationPeriod period) => period switch
    {
        GamificationPeriod.Week => "7 Tage",
        GamificationPeriod.Month => "30 Tage",
        _ => "Gesamt",
    };
}
