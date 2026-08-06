using MudBlazor;

namespace NOOSE_Website.Models.Enums;

/// <summary>How an agent abduction ended.</summary>
public enum AbductionOutcome
{
    StillHeld = 0,
    Escaped = 1,
    Rescued = 2,
    Released = 3,
    Killed = 4,
    RansomPaid = 5,
}

/// <summary>Display labels, icons and chip colors.</summary>
public static class AbductionOutcomeDisplay
{
    public static string Name(AbductionOutcome outcome) => outcome switch
    {
        AbductionOutcome.StillHeld => "In Gefangenschaft",
        AbductionOutcome.Escaped => "Geflohen",
        AbductionOutcome.Rescued => "Befreit",
        AbductionOutcome.Released => "Freigelassen",
        AbductionOutcome.Killed => "Getötet",
        AbductionOutcome.RansomPaid => "Lösegeld gezahlt",
        _ => "—",
    };

    public static string Icon(AbductionOutcome outcome) => outcome switch
    {
        AbductionOutcome.StillHeld => Icons.Material.Filled.Lock,
        AbductionOutcome.Escaped => Icons.Material.Filled.DirectionsRun,
        AbductionOutcome.Rescued => Icons.Material.Filled.Shield,
        AbductionOutcome.Released => Icons.Material.Filled.LockOpen,
        AbductionOutcome.Killed => Icons.Material.Filled.Dangerous,
        AbductionOutcome.RansomPaid => Icons.Material.Filled.Paid,
        _ => Icons.Material.Filled.HelpOutline,
    };

    public static Color ChipColor(AbductionOutcome outcome) => outcome switch
    {
        AbductionOutcome.StillHeld => Color.Error,
        AbductionOutcome.Escaped => Color.Warning,
        AbductionOutcome.Rescued => Color.Success,
        AbductionOutcome.Released => Color.Info,
        AbductionOutcome.Killed => Color.Error,
        AbductionOutcome.RansomPaid => Color.Warning,
        _ => Color.Default,
    };

    public static readonly IReadOnlyList<AbductionOutcome> All = new[]
    {
        AbductionOutcome.StillHeld,
        AbductionOutcome.Escaped,
        AbductionOutcome.Rescued,
        AbductionOutcome.Released,
        AbductionOutcome.Killed,
        AbductionOutcome.RansomPaid,
    };
}
