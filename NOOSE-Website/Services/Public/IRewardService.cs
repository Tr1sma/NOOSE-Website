using System.Security.Claims;
using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Services.Public;

/// <summary>Paying a bounty out to the citizens whose tips earned it, and the receipt they keep.</summary>
/// <remarks>
/// Owns <c>HinweisBelohnungen</c> and is the only writer of <c>BountyShareStatus.Ausgezahlt</c>. Its own service rather
/// than a method on <see cref="IBountyService"/> for one reason: this is the first money path with two audiences, and
/// every method of the bounty service starts from an internal-only guard. Pattern of <see cref="ITipService"/>.
/// <para>
/// The payout settles a notice once. It requires the notice to be <c>Gefasst</c> — marking that stays the wanted
/// service's own write, which owns that table — and afterwards every advertised share is <c>Ausgezahlt</c>, whether or
/// not it was drawn on to the last dollar. That status set is the idempotency token: a second payout finds nothing to
/// pay and says so.
/// </para>
/// </remarks>
public interface IRewardService
{
    /// <summary>What the payout dialog needs: the money, the payable tips, and why the others are not.</summary>
    Task<RewardDraft> GetDraftAsync(string wantedId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Paid slices of one notice, newest first; empty when the actor may not read its file.</summary>
    Task<IReadOnlyList<RewardRow>> GetForNoticeAsync(string wantedId, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>Paid slices of one tip; empty when it was never rewarded.</summary>
    Task<IReadOnlyList<RewardRow>> GetForTipAsync(string tipId, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>Pays the bounty out across one or more tips and returns the receipt number per tip.</summary>
    /// <remarks>One context, one transaction: no money without the status changes, no status change without the money.</remarks>
    Task<IReadOnlyList<string>> PayoutAsync(RewardPayoutInput input, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>The caller's own rewards; empty for an account without a civilian profile.</summary>
    Task<IReadOnlyList<CitizenRewardRow>> GetOwnAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>One receipt, for the citizen it belongs to or for leadership; null for anybody else.</summary>
    Task<CitizenRewardReceipt?> GetReceiptAsync(string receiptNumber, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);
}
