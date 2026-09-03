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

    /// <summary>Lowest order a capture report may end up with.</summary>
    /// <remarks>
    /// A floor rather than a fixed value, so the capture of a high-bounty target still outranks the capture of a
    /// nobody — except while someone is holding the person, where it is the ceiling outright. Anything less lets a
    /// maximal observation (5 × 5 × 4) overtake a handover in progress, and a handover does not become less urgent
    /// because nobody put a bounty on the notice. Among those the inbox falls back to age, which is the right
    /// tie-break for people standing next to a suspect.
    /// <para>
    /// A handed-over report keeps a floor below <see cref="Max"/>: it is high because money hangs off it, but nobody
    /// is at risk any more. <see cref="Max"/> itself never moves — it is also the cap of a hand-set priority.
    /// </para>
    /// </remarks>
    public static int Floor(TipKind kind, TipHandover? handover) => (kind, handover) switch
    {
        (TipKind.Ergreifung, TipHandover.Festgehalten) => Max,
        (TipKind.Ergreifung, _) => 70,
        _ => 0,
    };

    public static int Compute(decimal bountyTotal, HazardLevel? hazard, int confirmedTips,
        TipKind kind = TipKind.Beobachtung, TipHandover? handover = null)
        => Math.Max(
            BountyBand(bountyTotal) * HazardBand(hazard) * TipTrust.Tier(confirmedTips),
            Floor(kind, handover));
}
