using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services.Public;

/// <summary>How a bounty is split across the tips that earned it: the one rule, in memory, DI-free.</summary>
/// <remarks>
/// The share order is the whole point. Money that is already in the till or committed by the agency flows on its own;
/// a merely pledged private share needs an agent to physically hand cash over, which is the weakest cover there is, so
/// it is drawn on last. Within each group the oldest share goes first, and the id breaks a tie — the same payout must
/// always produce the same bookings.
/// </remarks>
public static class RewardAllocation
{
    /// <summary>Tips per payout; beyond this the dialog is not a form any more.</summary>
    public const int MaxTips = 10;

    /// <summary>One advertised share and what it can still cover.</summary>
    public sealed record ShareCapacity(string ShareId, decimal Amount, BountyOrigin Origin,
        BountyShareStatus Status, KassenKonto? Account, DateTime Timestamp);

    /// <summary>What one tip is to be paid.</summary>
    public sealed record TipDemand(string TipId, decimal Amount);

    /// <summary>One paid slice: this much of this share goes to this tip.</summary>
    public sealed record Slice(string ShareId, string TipId, decimal Amount);

    /// <summary>Whether paying this share out moves money out of a cash account.</summary>
    /// <remarks>
    /// Agency money always lies in the till. A private share does only once it was handed in; a pledged one never
    /// reached the agency, so the donor pays the citizen directly and no booking exists to make.
    /// </remarks>
    public static bool NeedsBooking(BountyOrigin origin, BountyShareStatus status)
        => origin == BountyOrigin.NooseKasse || status == BountyShareStatus.Gesichert;

    /// <summary>The draw order: bookable money first, oldest first, id as the tie-breaker.</summary>
    public static IReadOnlyList<ShareCapacity> Order(IEnumerable<ShareCapacity> shares)
        => shares
            .OrderBy(s => NeedsBooking(s.Origin, s.Status) ? 0 : 1)
            .ThenBy(s => s.Timestamp)
            .ThenBy(s => s.ShareId, StringComparer.Ordinal)
            .ToList();

    /// <summary>Splits the demands across the shares, or throws with the reason it cannot.</summary>
    public static IReadOnlyList<Slice> Distribute(IReadOnlyList<ShareCapacity> shares, IReadOnlyList<TipDemand> tips)
    {
        if (tips.Count == 0)
        {
            throw new InvalidOperationException("Bitte mindestens einen Hinweis für die Belohnung auswählen.");
        }
        if (tips.Count > MaxTips)
        {
            throw new InvalidOperationException($"Eine Auszahlung verteilt auf höchstens {MaxTips} Hinweise.");
        }
        if (tips.Select(t => t.TipId).Distinct(StringComparer.Ordinal).Count() != tips.Count)
        {
            throw new InvalidOperationException("Ein Hinweis darf nur einmal in derselben Auszahlung stehen.");
        }
        foreach (var tip in tips)
        {
            if (tip.Amount <= 0m)
            {
                throw new InvalidOperationException("Jeder belohnte Hinweis braucht einen Betrag größer 0.");
            }
            // the column holds two decimals; a third would be truncated by the database, not refused
            if (decimal.Round(tip.Amount, 2) != tip.Amount)
            {
                throw new InvalidOperationException("Ein Betrag hat höchstens zwei Dezimalstellen.");
            }
        }
        if (shares.Count == 0)
        {
            throw new InvalidOperationException(
                "Auf diese Ausschreibung ist kein Kopfgeld ausgesetzt oder es ist bereits ausgezahlt.");
        }

        var available = shares.Sum(s => s.Amount);
        var demanded = tips.Sum(t => t.Amount);
        if (demanded > available)
        {
            throw new InvalidOperationException(
                $"Die Belohnung ({Money.Format(demanded)}) übersteigt das ausgesetzte Kopfgeld ({Money.Format(available)}).");
        }

        var ordered = Order(shares);
        var left = ordered.ToDictionary(s => s.ShareId, s => s.Amount, StringComparer.Ordinal);
        var slices = new List<Slice>();

        foreach (var tip in tips)
        {
            var open = tip.Amount;
            foreach (var share in ordered)
            {
                if (open <= 0m)
                {
                    break;
                }
                var rest = left[share.ShareId];
                if (rest <= 0m)
                {
                    continue;
                }
                var take = Math.Min(open, rest);
                slices.Add(new Slice(share.ShareId, tip.TipId, take));
                left[share.ShareId] = rest - take;
                open -= take;
            }
            if (open > 0m)
            {
                // unreachable while the sum check above holds; kept so a future edit of that check cannot pay
                // out money that does not exist
                throw new InvalidOperationException("Das ausgesetzte Kopfgeld deckt diese Verteilung nicht.");
            }
        }

        return slices;
    }
}
