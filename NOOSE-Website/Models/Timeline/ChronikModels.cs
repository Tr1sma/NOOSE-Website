namespace NOOSE_Website.Models.Timeline;

/// <summary>Window + filters for the agency-wide chronicle.</summary>
public sealed record ChronikQuery(DateTime FromUtc, DateTime ToUtc, string? TypeFilter = null, string? AgentId = null);

/// <summary>One record-level event on the global chronicle.</summary>
public sealed record ChronikEvent(
    DateTime Timestamp, TimelineCategory Category, string EntityType, string EntityId,
    string Name, string Title, string? ActorName, string? Href);

/// <summary>Chronicle page: events (newest first) + whether the window was capped.</summary>
public sealed record ChronikResult(IReadOnlyList<ChronikEvent> Events, bool Truncated);

/// <summary>An agent selectable in the chronicle filter.</summary>
public sealed record ChronikAgentOption(string Id, string Name);

/// <summary>Filter options for the chronicle (record types + acting agents).</summary>
public sealed record ChronikFilterOptions(IReadOnlyList<string> Types, IReadOnlyList<ChronikAgentOption> Agents);
