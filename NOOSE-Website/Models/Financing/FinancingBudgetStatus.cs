using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Financing;

/// <summary>An agent's funding budget for one calendar month, as shown on the budget tiles.</summary>
public record FinancingBudgetStatus(
    string AgentId,
    string Codename,
    Rank? Rank,
    int Year,
    int Month,
    decimal BaseBudget,
    decimal CarryIn,
    decimal Consumed,
    int CarryPercent,
    bool IsOverride)
{
    /// <summary>Base plus what last month handed over.</summary>
    public decimal Available => BaseBudget + CarryIn;

    /// <summary>What is left; negative once leadership deliberately overran the budget.</summary>
    public decimal Remaining => Available - Consumed;

    public static FinancingBudgetStatus Empty { get; } =
        new(string.Empty, string.Empty, null, 0, 0, 0m, 0m, 0m, 0, false);
}
