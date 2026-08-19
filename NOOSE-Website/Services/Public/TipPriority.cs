using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services.Public;

/// <summary>The one formula behind the inbox order: advertised bounty times public hazard level times trust tier.</summary>
/// <remarks>
/// Three bands, each with a floor of 1. Multiplied literally, a tip on a notice without bounty would score 0 and a
/// critical hazard would sort below a triviality. The floor keeps every factor able to raise but never to erase.
/// Consequence, accepted: a tip without a notice scores its trust tier alone and sits below every referenced tip —
/// the inbox already separates by status, so nothing is buried by it.
/// </remarks>
public static class TipPriority
{
    public const int Min = 1;

    public const int Max = 25 * TipTrust.MaxTier;

    /// <summary>Advertised sum in dollars to a band 1..5.</summary>
    public static int BountyBand(decimal bountyTotal) => bountyTotal switch
    {
        <= 0m => 1,
        < 5_000m => 2,
        < 25_000m => 3,
        < 100_000m => 4,
        _ => 5,
    };

    /// <summary>Published hazard level to a band 1..5; the raw score never enters this.</summary>
    public static int HazardBand(HazardLevel? hazard) => (int)(hazard ?? HazardLevel.No) + 1;

    public static int Compute(decimal bountyTotal, HazardLevel? hazard, int confirmedTips)
        => BountyBand(bountyTotal) * HazardBand(hazard) * TipTrust.Tier(confirmedTips);
}
