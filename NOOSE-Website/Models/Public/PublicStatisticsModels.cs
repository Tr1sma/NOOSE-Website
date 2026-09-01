namespace NOOSE_Website.Models.Public;

/// <summary>The figures the agency publishes about its own work.</summary>
/// <remarks>
/// Outward. Every member is nullable, and null means "this set is not published" rather than zero. Zero is a claim —
/// "nobody has been caught" — and a switched-off module has made no claim at all; the same reason the situation level
/// answers null instead of Niedrig.
/// <para>
/// Structurally carries no record, no name, no case number and no per-notice figure. A tip count for one notice would
/// be a public register of how much attention one person draws, and an aggregate is only safe as long as it cannot be
/// attributed.
/// </para>
/// </remarks>
public sealed record PublicStatistics(
    int? OpenNotices,
    int? CapturedNotices,
    int? TipsReceived,
    int? TipsConfirmed,
    int? TipsLedToCapture,
    decimal? RewardsPaid)
{
    public static PublicStatistics Empty { get; } = new(null, null, null, null, null, null);

    /// <summary>True when at least one figure is published; the start page renders no band otherwise.</summary>
    public bool HasAny
        => OpenNotices is not null || CapturedNotices is not null || TipsReceived is not null
            || TipsConfirmed is not null || TipsLedToCapture is not null || RewardsPaid is not null;
}
