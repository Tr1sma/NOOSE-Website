using MudBlazor;

namespace NOOSE_Website.Models.Enums;

/// <summary>Reason an agent signs off for a date range.</summary>
public enum AbsenceCategory
{
    /// <summary>Holiday.</summary>
    Vacation = 0,
    /// <summary>Real-life work.</summary>
    Work = 1,
    /// <summary>Illness.</summary>
    Sick = 2,
    /// <summary>Break from roleplay.</summary>
    RpBreak = 3,
    /// <summary>Other reason.</summary>
    Misc = 4,
}

/// <summary>Display labels and icons.</summary>
public static class AbsenceCategoryDisplay
{
    public static string Name(AbsenceCategory category) => category switch
    {
        AbsenceCategory.Vacation => "Urlaub",
        AbsenceCategory.Work => "Arbeit (RL)",
        AbsenceCategory.Sick => "Krank",
        AbsenceCategory.RpBreak => "RP-Pause",
        AbsenceCategory.Misc => "Sonstiges",
        _ => "—",
    };

    public static string Icon(AbsenceCategory category) => category switch
    {
        AbsenceCategory.Vacation => Icons.Material.Filled.BeachAccess,
        AbsenceCategory.Work => Icons.Material.Filled.Work,
        AbsenceCategory.Sick => Icons.Material.Filled.LocalHospital,
        AbsenceCategory.RpBreak => Icons.Material.Filled.PauseCircle,
        AbsenceCategory.Misc => Icons.Material.Filled.MoreHoriz,
        _ => Icons.Material.Filled.EventBusy,
    };

    public static readonly IReadOnlyList<AbsenceCategory> All = new[]
    {
        AbsenceCategory.Vacation,
        AbsenceCategory.Work,
        AbsenceCategory.Sick,
        AbsenceCategory.RpBreak,
        AbsenceCategory.Misc,
    };
}
