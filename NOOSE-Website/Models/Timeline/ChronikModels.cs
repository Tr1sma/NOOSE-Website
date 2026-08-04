using NOOSE_Website.Services;

namespace NOOSE_Website.Models.Timeline;

/// <summary>Window, filters and page cursor for the agency-wide chronicle.</summary>
public sealed record ChronikQuery(
    DateTime FromUtc,
    DateTime ToUtc,
    IReadOnlyList<string>? Types = null,
    IReadOnlyList<string>? AgentIds = null,
    IReadOnlyList<TimelineCategory>? Categories = null,
    string? Text = null,
    DateTime? BeforeUtc = null,
    int MinEvents = 40);

/// <summary>One record-level event on the global chronicle; always anchored on a record the viewer may see.</summary>
public sealed record ChronikEvent(
    DateTime Timestamp,
    TimelineCategory Category,
    string EntityType,
    string EntityId,
    string Name,
    string Title,
    string? Detail,
    string? ActorName,
    string? Href,
    bool RecordDeleted,
    IReadOnlyList<AuditDisplay.FieldChange>? Changes = null);

/// <summary>Chronicle page: events newest first, plus the exclusive upper bound of the next page.</summary>
public sealed record ChronikResult(
    IReadOnlyList<ChronikEvent> Events,
    DateTime? NextCursorUtc,
    bool HasMore);

/// <summary>One bar of the density band: bucket start (UTC) and raw event count.</summary>
public sealed record ChronikDensityBucket(DateTime StartUtc, int Count);

/// <summary>An agent selectable in the chronicle filter.</summary>
public sealed record ChronikAgentOption(string Id, string Name);

/// <summary>Filter options for the chronicle (record types + acting agents).</summary>
public sealed record ChronikFilterOptions(IReadOnlyList<string> Types, IReadOnlyList<ChronikAgentOption> Agents);
