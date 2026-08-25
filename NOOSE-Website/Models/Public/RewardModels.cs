using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Public;

// ---- inward: the payout desk ----

/// <summary>One paid slice as the internal panels show it.</summary>
/// <remarks>
/// One row per (tip, share), so origin, account and booking are visible where the money is worked. The citizen name
/// stays null while the anonymity promise holds — a payout requires it to be resolved, so a blank here means the tip
/// was rewarded before that rule existed, not that the row is broken.
/// </remarks>
public record RewardRow(
    string ReceiptNumber,
    string TipId,
    string TipCaseNumber,
    string? CitizenName,
    decimal Amount,
    BountyOrigin Origin,
    KassenKonto? Account,
    string? BookingCaseNumber,
    bool SelfPaid,
    DateTime PaidAt);

/// <summary>Everything the payout dialog needs before a single figure is entered.</summary>
public record RewardDraft(
    string WantedId,
    string? WantedCaseNumber,
    string DisplayName,
    bool IsCaptured,
    decimal Available,
    decimal Bookable,
    decimal Handover,
    IReadOnlyList<RewardDraftTip> Payable,
    IReadOnlyList<RewardDraftBlocked> Blocked,
    bool AlreadyPaid);

/// <summary>A tip that can be rewarded right now.</summary>
public record RewardDraftTip(
    string TipId,
    string CaseNumber,
    string CitizenName,
    int TrustTier,
    DateTime CreatedAt,
    string Excerpt);

/// <summary>A tip on this notice that cannot be rewarded, with the reason the dialog shows.</summary>
public record RewardDraftBlocked(string TipId, string CaseNumber, string Reason);

/// <summary>What the dialog posts back.</summary>
public class RewardPayoutInput
{
    public string WantedId { get; set; } = string.Empty;
    public List<RewardTipAmount> Tips { get; set; } = [];
}

/// <summary>One line of the split.</summary>
public class RewardTipAmount
{
    public string TipId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

/// <summary>A rewarded tip and what its post-commit follow-ups need.</summary>
public record TipRewardTarget(string TipId, string CaseNumber, string CitizenProfileId);

// ---- outward: what the citizen sees of their own reward ----

/// <summary>One reward of the citizen, at their own tip.</summary>
public record CitizenRewardRow(string ReceiptNumber, string TipCaseNumber, decimal Amount, DateTime PaidAt);

/// <summary>The printable receipt; the paying agent appears nowhere on it, because this record cannot carry them.</summary>
public record CitizenRewardReceipt(
    string ReceiptNumber,
    string TipCaseNumber,
    string? WantedCaseNumber,
    string RecipientName,
    decimal Amount,
    DateTime PaidAt);
