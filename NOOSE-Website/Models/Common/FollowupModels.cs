namespace NOOSE_Website.Models.Common;

/// <summary>Input for a followup.</summary>
public sealed record FollowupInput(DateTime DueAt, string? Note, string? ResponsibleAgentId);

/// <summary>A followup prepared for the record panel.</summary>
public sealed record FollowupItem(
    string Id,
    DateTime DueAt,
    string? Note,
    string? ResponsibleAgentId,
    string? ResponsibleCodename,
    bool Done,
    DateTime? DoneAt,
    bool Overdue,
    bool MayEdit);

/// <summary>A due followup resolved for the dashboard list.</summary>
public sealed record FollowupDashboardItem(
    string Id,
    string Display,
    string? Href,
    DateTime DueAt,
    string? Note);
