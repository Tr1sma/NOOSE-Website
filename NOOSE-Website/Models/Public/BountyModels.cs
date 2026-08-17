using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Public;

/// <summary>One share of a bounty as the internal panel shows it.</summary>
/// <remarks>
/// Inward only. The donor appears as a codename, never as a real name — the same rule that keeps agents anonymous
/// outside also keeps a bounty list from becoming a real-name roster inside.
/// </remarks>
public sealed record BountyShareRow(
    string Id,
    BountyOrigin Origin,
    decimal Amount,
    KassenKonto? Account,
    string DonorName,
    bool IsOwn,
    BountyShareStatus Status,
    DateTime Timestamp,
    string? BookingCaseNumber,
    string? WithdrawnReason);

/// <summary>The money on one head, broken down for the people who are allowed to see the breakdown.</summary>
public sealed record BountySummary(
    decimal Advertised,
    decimal Pending,
    decimal Official,
    decimal Private,
    decimal Secured,
    int ShareCount,
    bool IsCap)
{
    public static BountySummary Empty { get; } = new(0m, 0m, 0m, 0m, 0m, 0, false);
}

/// <summary>What one cash account owes in bounties against what it holds.</summary>
/// <remarks>
/// <paramref name="Owed"/> counts official pledges <em>and</em> private money already handed in: a deposit raises the
/// balance and is nevertheless spoken for, so leaving it out reports an all-clear that does not exist.
/// </remarks>
public sealed record BountyCoverage(KassenKonto Account, decimal Owed, decimal Balance)
{
    /// <summary>How much is missing; zero or less means covered.</summary>
    public decimal Shortfall => Owed - Balance;

    public bool IsShort => Shortfall > 0m;
}

/// <summary>One pending official-bounty request in the approval inbox.</summary>
public sealed record BountyRequestRow(
    string RequestId,
    string ShareId,
    string DisplayName,
    string TargetDesignation,
    decimal Amount,
    KassenKonto? Account,
    string? RequesterName,
    string? Justification,
    DateTime CreatedAt);

/// <summary>What filing an official share did: committed straight away, or turned into a request.</summary>
public enum BountyAddOutcome
{
    Committed = 0,
    Requested = 1,
}
