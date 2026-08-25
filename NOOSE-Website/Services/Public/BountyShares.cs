using System.Linq.Expressions;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services.Public;

/// <summary>The one rule for which shares are money on a head, in the two forms it is needed in.</summary>
/// <remarks>
/// The sum reaches the outside, so the rule must not exist twice: the public snapshot, the internal breakdown and the
/// before/after of a raise all name this. Pattern of <c>AgentSelection</c> — a query predicate plus its in-memory twin.
/// </remarks>
public static class BountyShares
{
    /// <summary>Pledged or secured; a pending share is an open internal decision, a paid one is spent.</summary>
    public static readonly Expression<Func<FahndungKopfgeldAnteil, bool>> Advertised =
        k => k.Status == BountyShareStatus.Zugesagt || k.Status == BountyShareStatus.Gesichert;

    /// <summary>In-memory twin of <see cref="Advertised"/>.</summary>
    public static bool IsAdvertised(BountyShareStatus status)
        => status is BountyShareStatus.Zugesagt or BountyShareStatus.Gesichert;
}
