namespace NOOSE_Website.Models.CounterIntel;

/// <summary>One access-log row reduced to what the insider-threat rules need (timestamp already local).</summary>
public readonly record struct AccessRow(string AgentId, string? AgentName, DateTime LocalTimestamp, string EntityType, string EntityId);

/// <summary>KPI summary of the access log over the window.</summary>
public sealed record CounterIntelOverview(int TotalAccesses, int DistinctAgents, int DistinctRecords, int OffHoursAccesses, int WindowDays);

/// <summary>One agent's 24-hour access histogram.</summary>
public sealed record HeatAgent(string AgentId, string AgentName, IReadOnlyList<int> Hours);

/// <summary>Access heatmap (agent × hour-of-day) + the maximum cell for intensity scaling.</summary>
public sealed record CounterIntelHeatmap(IReadOnlyList<HeatAgent> Agents, int MaxCount);

/// <summary>Access count for one record type.</summary>
public sealed record TypeCount(string EntityType, int Count);

/// <summary>A single recent access.</summary>
public sealed record RecentAccess(DateTime WhenLocal, string EntityType, string EntityId, string? Href);

/// <summary>An agent's access profile.</summary>
public sealed record AgentAccessProfile(
    string AgentId, string AgentName, int Total, int DistinctRecords, int OffHours,
    IReadOnlyList<TypeCount> ByType, IReadOnlyList<RecentAccess> Recent);

/// <summary>A flagged insider-threat pattern.</summary>
public sealed record InsiderFlag(string AgentId, string AgentName, string Rule, string Detail, int Severity, string? Href);

/// <summary>An agent selectable in the cockpit.</summary>
public sealed record AgentOption(string Id, string Name);
