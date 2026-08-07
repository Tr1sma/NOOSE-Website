using MudBlazor;

namespace NOOSE_Website.Models.Enums;

/// <summary>Why a NOOSEI request or an agent's week was flagged.</summary>
public enum LlmAnomalyKind
{
    /// <summary>A single request costing a multiple of the rolling average.</summary>
    CostSpike = 0,

    /// <summary>A large share of the weekly quota burned inside a short window.</summary>
    BurnRate = 1,

    /// <summary>Many requests in few minutes, several of them near-identical.</summary>
    Burst = 2,

    /// <summary>A week far above the agent's own trailing average or their rank's.</summary>
    Outlier = 3,
}

/// <summary>German labels, icons and colours of <see cref="LlmAnomalyKind"/>.</summary>
public static class LlmAnomalyKindDisplay
{
    public static readonly LlmAnomalyKind[] All =
        [LlmAnomalyKind.CostSpike, LlmAnomalyKind.BurnRate, LlmAnomalyKind.Burst, LlmAnomalyKind.Outlier];

    public static string Name(LlmAnomalyKind kind) => kind switch
    {
        LlmAnomalyKind.CostSpike => "Kostenausreißer",
        LlmAnomalyKind.BurnRate => "Schneller Verbrauch",
        LlmAnomalyKind.Burst => "Anfrage-Serie",
        LlmAnomalyKind.Outlier => "Verbrauchs-Ausreißer",
        _ => kind.ToString(),
    };

    public static string Icon(LlmAnomalyKind kind) => kind switch
    {
        LlmAnomalyKind.CostSpike => Icons.Material.Filled.TrendingUp,
        LlmAnomalyKind.BurnRate => Icons.Material.Filled.LocalFireDepartment,
        LlmAnomalyKind.Burst => Icons.Material.Filled.FastForward,
        LlmAnomalyKind.Outlier => Icons.Material.Filled.ShowChart,
        _ => Icons.Material.Filled.PriorityHigh,
    };
}
