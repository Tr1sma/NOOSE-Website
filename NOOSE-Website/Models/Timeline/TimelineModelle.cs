using NOOSE_Website.Services;

namespace NOOSE_Website.Models.Timeline;

/// <summary>Timeline event category; drives filter chip, icon and colour.</summary>
public enum TimelineCategory
{
    Asset,
    Change,
    Deletion,
    Restoration,
    Classification,
    Doc,
    Observation,
    Photo,
    Relation,
    Membership,
    Allocation,
    Link,
    Comment,
    Source,
    Followup,
    Activity,
}

/// <summary>One event in the unified record timeline; Timestamp is UTC (sort key), Changes only for audit events.</summary>
public sealed record TimelineEntry(
    DateTime Timestamp,
    TimelineCategory Category,
    string Title,
    string? Detail,
    string? ActorName,
    string? Href,
    IReadOnlyList<AuditDisplay.FieldChange>? Changes = null);
