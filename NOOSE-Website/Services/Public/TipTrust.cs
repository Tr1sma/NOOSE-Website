namespace NOOSE_Website.Services.Public;

/// <summary>Trust tier of a tipster, derived from confirmed tips; drives the daily quota and the priority band.</summary>
/// <remarks>
/// A tier rather than the raw count, because the count is a recognition mark: it is withheld from the handler while an
/// anonymity promise holds, while the tier is not — the promise covers the identity, not the track record, and the tier
/// is a visible factor of the priority anyway.
/// </remarks>
public static class TipTrust
{
    /// <summary>Lowest and highest tier; the priority band is the tier itself.</summary>
    public const int MinTier = 1;

    public const int MaxTier = 4;

    public static int Tier(int confirmedTips) => confirmedTips switch
    {
        <= 0 => 1,
        < 3 => 2,
        < 10 => 3,
        _ => 4,
    };

    public static string Label(int tier) => tier switch
    {
        <= 1 => "Neu",
        2 => "Bekannt",
        3 => "Verlässlich",
        _ => "Vertraut",
    };

    /// <summary>Submissions per rolling 24 hours at this tier.</summary>
    public static int DailyQuota(int tier) => tier switch
    {
        <= 1 => TipRules.PerDay,
        2 => 8,
        3 => 12,
        _ => 20,
    };

    /// <summary>Quota for a tipster with this many confirmed tips.</summary>
    public static int QuotaFor(int confirmedTips) => DailyQuota(Tier(confirmedTips));
}
