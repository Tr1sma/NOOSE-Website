namespace NOOSE_Website.Models.Public;

/// <summary>What the public area produced over one window, for the leadership panel.</summary>
/// <remarks>
/// Inward, every record in this file. The figures are per notice and per desk, which is exactly what the outward
/// <see cref="PublicStatistics"/> refuses to carry — the rule there is that an aggregate is only safe while it
/// cannot be attributed, and inside the house attribution is the point.
/// </remarks>
public sealed record PublicKpiReport(
    int Days,
    PublicKpiTips Tips,
    PublicKpiRewards Rewards,
    PublicKpiTickets Tickets,
    PublicKpiViews? Views)
{
    public static PublicKpiReport Empty { get; } =
        new(0, PublicKpiTips.Empty, PublicKpiRewards.Empty, PublicKpiTickets.Empty, null);
}

/// <summary>Tip throughput of the window.</summary>
/// <param name="Decided">Confirmed, rejected or led to an arrest — the honest denominator of a success rate.</param>
public sealed record PublicKpiTips(int Received, int Confirmed, int Captures, int Decided, int Open)
{
    public static PublicKpiTips Empty { get; } = new(0, 0, 0, 0, 0);

    /// <summary>Share of decided tips that ended in an arrest. Open tips are not failures.</summary>
    public double CaptureShare => Decided <= 0 ? 0 : (double)Captures / Decided;
}

/// <summary>What the agency paid out for tips in the window.</summary>
/// <param name="PaidCaptures">Notices a reward was booked against in the window; the denominator of the cost.</param>
/// <param name="Captures">Arrests made in the window — a different cohort from the payouts, on purpose.</param>
/// <param name="RewardedCaptures">Of those arrests, how many were rewarded at any time; the share's numerator.</param>
public sealed record PublicKpiRewards(
    decimal Paid, decimal FromTill, decimal HandedOver, int PaidCaptures, int Captures, int RewardedCaptures)
{
    public static PublicKpiRewards Empty { get; } = new(0m, 0m, 0m, 0, 0, 0);

    /// <summary>Reward per PAID arrest, null when none was paid — never a division over all arrests.</summary>
    /// <remarks>An average over arrests that cost nothing describes a different agency than the one being asked about.</remarks>
    public decimal? PerPaidCapture => PaidCaptures <= 0 ? null : Paid / PaidCaptures;

    /// <summary>Share of the window's arrests that were rewarded.</summary>
    /// <remarks>
    /// Measured over the arrests, not over the payouts: a payout made in the window can belong to an arrest from
    /// before it, so dividing the payout cohort by the arrest cohort produced shares above 100 %.
    /// </remarks>
    public double RewardedShare => Captures <= 0 ? 0 : (double)RewardedCaptures / Captures;
}

/// <summary>How fast the leadership desk answered in the window.</summary>
/// <param name="MedianReplyMinutes">Time to the first HUMAN agency reply; the entry confirmation does not count.
/// Null when nothing was answered in the window — "not measured" and "answered instantly" are different claims.</param>
public sealed record PublicKpiTickets(
    int Opened, int Answered, int Waiting, int? MedianReplyMinutes, int? P95ReplyMinutes, int? OldestWaitingMinutes)
{
    public static PublicKpiTickets Empty { get; } = new(0, 0, 0, null, null, null);
}

/// <summary>How much attention the published notices drew.</summary>
/// <remarks>
/// Null on the report, never zero, when the reader may not open the notice cross-list: "you may not read this" and
/// "nobody looked" are different statements. The top list names notices, so it passes the same record gate the
/// management list applies.
/// </remarks>
public sealed record PublicKpiViews(
    long Total, int Median, int P90, int Notices, IReadOnlyList<PublicKpiNoticeViews> Top);

/// <summary>One notice in the attention ranking.</summary>
public sealed record PublicKpiNoticeViews(string CaseNumber, string DisplayName, int ViewCount, DateTime? PublishedAt);
