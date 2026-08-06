using MudBlazor;

namespace NOOSE_Website.Models.Enums;

/// <summary>Stage of a funding request: requested, decided, and finally paid out.</summary>
public enum FinancingStatus
{
    Requested = 0,
    Approved = 1,
    Rejected = 2,
    Paid = 3,
    Withdrawn = 4,
}

/// <summary>Display labels, icons and chip colours per funding-request stage.</summary>
public static class FinancingStatusDisplay
{
    public static string Name(FinancingStatus status) => status switch
    {
        FinancingStatus.Requested => "Beantragt",
        FinancingStatus.Approved => "Angenommen",
        FinancingStatus.Rejected => "Abgelehnt",
        FinancingStatus.Paid => "Ausgezahlt",
        FinancingStatus.Withdrawn => "Zurückgezogen",
        _ => "—",
    };

    public static string Icon(FinancingStatus status) => status switch
    {
        FinancingStatus.Requested => Icons.Material.Filled.HourglassEmpty,
        FinancingStatus.Approved => Icons.Material.Filled.Check,
        FinancingStatus.Rejected => Icons.Material.Filled.Close,
        FinancingStatus.Paid => Icons.Material.Filled.Payments,
        FinancingStatus.Withdrawn => Icons.Material.Filled.Undo,
        _ => Icons.Material.Filled.RequestQuote,
    };

    public static Color ChipColor(FinancingStatus status) => status switch
    {
        FinancingStatus.Requested => Color.Info,
        FinancingStatus.Approved => Color.Warning,
        FinancingStatus.Rejected => Color.Error,
        FinancingStatus.Paid => Color.Success,
        FinancingStatus.Withdrawn => Color.Default,
        _ => Color.Default,
    };

    /// <summary>Awaiting a decision, so it shows up in the leadership inbox and the nav badge.</summary>
    public static bool IsOpen(FinancingStatus status) => status == FinancingStatus.Requested;

    /// <summary>Reserves the agent's monthly budget; approval reserves it, payout keeps it reserved.</summary>
    public static bool ConsumesBudget(FinancingStatus status)
        => status is FinancingStatus.Approved or FinancingStatus.Paid;

    /// <summary>No further transition is possible; a rejection stays reversible, a withdrawal does not.</summary>
    public static bool IsTerminal(FinancingStatus status) => status == FinancingStatus.Withdrawn;

    public static readonly IReadOnlyList<FinancingStatus> All = new[]
    {
        FinancingStatus.Requested,
        FinancingStatus.Approved,
        FinancingStatus.Rejected,
        FinancingStatus.Paid,
        FinancingStatus.Withdrawn,
    };
}
