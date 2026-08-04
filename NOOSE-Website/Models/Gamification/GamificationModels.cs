namespace NOOSE_Website.Models.Gamification;

/// <summary>Per-agent contribution counters (soft-deleted records do not count — global query filter).</summary>
public sealed record AgentStats(
    int Records, int Docs, int Links, int Classifications, int Observations, int SolvedCases, int Badges)
{
    public static readonly AgentStats Empty = new(0, 0, 0, 0, 0, 0, 0);

    /// <summary>Weighted contribution score used for the leaderboard ranking.</summary>
    public int Points => Records * 3 + Docs * 2 + Links + Classifications * 2 + Observations + SolvedCases * 5;
}

/// <summary>One ranked row on the leaderboard (all five scored dimensions are surfaced so Points reconcile with the columns).</summary>
public sealed record LeaderboardEntry(
    int Position, string AgentId, string Codename, int Points,
    int Records, int Docs, int Links, int Classifications, int Observations, int SolvedCases);

/// <summary>An earned badge resolved against the catalog for display.</summary>
public sealed record BadgeView(string Key, string Label, string Icon, string Description, DateTime AwardedAt);
