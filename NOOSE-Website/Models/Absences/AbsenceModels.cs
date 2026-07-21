using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Absences;

/// <summary>Form model for filing or editing an absence.</summary>
public class AbsenceInput
{
    /// <summary>First day off, local; the service converts to a calendar day.</summary>
    public DateTime? From { get; set; }

    /// <summary>Last day off, inclusive.</summary>
    public DateTime? To { get; set; }

    public AbsenceCategory Category { get; set; } = AbsenceCategory.Vacation;

    /// <summary>Free text; leadership only.</summary>
    public string? Reason { get; set; }
}

/// <summary>One absence as shown in a list; Reason is already nulled for viewers who may not read it.</summary>
public record AbsenceRow(
    string Id,
    string AgentId,
    string Codename,
    DateOnly FromDate,
    DateOnly ToDate,
    int Days,
    AbsenceCategory Category,
    string? Reason,
    DateTime? AcknowledgedAt,
    string? AcknowledgedByName,
    bool MayEdit);

/// <summary>Per-agent absence totals for the leadership overview.</summary>
public record AbsenceAgentRow(
    string AgentId,
    string Codename,
    string Href,
    int Count,
    int Days,
    DateOnly? LastTo);
