using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Common;

/// <summary>Per partner-rank default visibility (record types + tabs). Stored as JSON in a SystemSetting row.</summary>
public sealed class PartnerVisibilityConfig
{
    /// <summary>Keyed by <see cref="RankKey"/>; a present key means allowlist mode for that rank.</summary>
    public Dictionary<string, PartnerRankVisibility> Ranks { get; set; } = new();

    /// <summary>Stable key for an (agency, rank) pair.</summary>
    public static string RankKey(PartnerAgency agency, PartnerRank rank) => $"{(int)agency}:{(int)rank}";
}

/// <summary>Allowlist for one partner rank.</summary>
public sealed class PartnerRankVisibility
{
    /// <summary>Visible record-type keys (CLR type names).</summary>
    public List<string> Types { get; set; } = new();

    /// <summary>Per type, the visible tab slugs; a missing entry for a listed type means all tabs.</summary>
    public Dictionary<string, List<string>> Tabs { get; set; } = new();
}
