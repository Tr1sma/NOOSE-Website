namespace NOOSE_Website.Models.Common;

/// <summary>Budgets and caps of the global search. Bound from configuration section "Search".</summary>
public sealed class SearchOptions
{
    public const string SectionName = "Search";

    /// <summary>Wall clock for the whole search. What is not done by then is reported missing, never dropped —
    /// a spinner that ends in a silently shorter list is the worst of the possible answers.</summary>
    public int BudgetMs { get; set; } = 8_000;

    /// <summary>Ceiling for a single category inside the whole budget, so one longtext scan cannot spend the
    /// window while cheap categories wait behind it in the concurrency queue.</summary>
    public int ProviderBudgetMs { get; set; } = 2_500;

    /// <summary>Providers running at once. Each holds one pooled MySQL connection for its lifetime.</summary>
    /// <remarks>Tests must set this to 1: the SQLite test harness hands every context the same open connection,
    /// and two concurrent commands on one SQLite connection are undefined.</remarks>
    public int MaxConcurrency { get; set; } = 4;

    /// <summary>Palette budget. It fires while the agent is still typing, so it gets a fraction of the page's.</summary>
    public int QuickBudgetMs { get; set; } = 300;

    /// <summary>Hits kept per category. A group that reaches it renders as "50+".</summary>
    public int PerCategory { get; set; } = 50;

    /// <summary>Cap on in-memory fuzzy candidates per category, to bound load.</summary>
    public int FuzzyCandidates { get; set; } = 2_000;
}
