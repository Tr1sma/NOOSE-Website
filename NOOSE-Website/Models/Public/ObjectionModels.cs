using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Public;

// ---- outward: what the citizen sees of their own objection ----

/// <summary>One of the citizen's own objections in their list.</summary>
/// <remarks>
/// Addressed by case number, never by row id, and it carries no agent at all: the decision is the agency's, not a
/// named agent's. What it does carry is the notice's own case number, because that is the thing the citizen objected
/// to and the only handle they ever had on it.
/// </remarks>
public record CitizenObjectionRow(
    string CaseNumber,
    string WantedCaseNumber,
    string WantedDisplayName,
    ObjectionStatus Status,
    string Text,
    string? DecisionNote,
    DateTime CreatedAt,
    DateTime? DecidedAt);

// ---- inward: the desk's view ----

/// <summary>One objection in the desk list.</summary>
/// <remarks>
/// No decider codename here: the list is a queue, and an identity that nothing renders has no business travelling.
/// The detail carries it, where it is actually shown.
/// </remarks>
public record ObjectionRow(
    string Id,
    string CaseNumber,
    string WantedCaseNumber,
    string WantedDisplayName,
    PublicWantedStatus WantedStatus,
    ObjectionStatus Status,
    string CitizenName,
    DateTime CreatedAt,
    DateTime? DecidedAt,
    bool HasCase);

/// <summary>One objection, opened by a handler.</summary>
public record ObjectionDetail(
    string Id,
    string CaseNumber,
    string WantedCaseNumber,
    string WantedDisplayName,
    PublicWantedStatus WantedStatus,
    ObjectionStatus Status,
    string Text,
    string CitizenName,
    bool CitizenIsBlocked,
    string? DecisionNote,
    string? DecidedByCodename,
    DateTime CreatedAt,
    DateTime? DecidedAt,
    string? LinkedCaseId,
    string? LinkedCaseNumber);

/// <summary>How many objections sit in each tab of the desk.</summary>
public record ObjectionCounts(int Open, int Decided);

/// <summary>Form input when a citizen files an objection.</summary>
public class ObjectionInput
{
    /// <summary>The notice's public case number; the row id never travels from outside.</summary>
    public string WantedCaseNumber { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}
