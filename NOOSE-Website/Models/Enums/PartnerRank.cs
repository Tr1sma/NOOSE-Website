namespace NOOSE_Website.Models.Enums;

/// <summary>Partner rank tier within an agency; gates rank-restricted partner pages.</summary>
public enum PartnerRank
{
    Member = 1,
    Special = 2,
    Chief = 3,
}

/// <summary>Display labels for partner ranks (agency + tier).</summary>
public static class PartnerRankDisplay
{
    /// <summary>Tier suffix; empty for the base tier.</summary>
    public static string Suffix(PartnerRank rank) => rank switch
    {
        PartnerRank.Special => "Special",
        PartnerRank.Chief => "Chief",
        _ => string.Empty,
    };

    /// <summary>Combined label, e.g. "LSPD", "LSPD Special", "LSPD Chief".</summary>
    public static string Full(PartnerAgency? agency, PartnerRank? rank)
    {
        var name = PartnerAgencyDisplay.Name(agency);
        var suffix = rank is { } r ? Suffix(r) : string.Empty;
        return string.IsNullOrEmpty(suffix) ? name : $"{name} {suffix}";
    }

    /// <summary>All tiers, lowest first.</summary>
    public static readonly IReadOnlyList<PartnerRank> All = new[]
    {
        PartnerRank.Member,
        PartnerRank.Special,
        PartnerRank.Chief,
    };
}
