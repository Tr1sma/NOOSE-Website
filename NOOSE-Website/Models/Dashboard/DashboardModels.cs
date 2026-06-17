using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Dashboard;

/// <summary>Record kind shown on the dashboard; drives icon, label and link.</summary>
public enum DashboardRecordType
{
    Person,
    Faction,
    PersonGroup,
    Party,
    Operation,
    Taskforce,
    Case,
}

/// <summary>Dashboard metric tiles; counts are classification-filtered per calling agent.</summary>
public record DashboardMetrics(
    int People,
    int FactionsAndGroups,
    int Operations,
    int OpenCases,
    int OpenRequests,
    int Classified,
    int StaleRecords);

/// <summary>A record due for update; classification/trash-filtered per caller.</summary>
public record DashboardStaleRecord(
    DashboardRecordType Type,
    string Name,
    string CaseNumber,
    string Href,
    RecencyLevel Level,
    DateTime ReferenceUtc);

/// <summary>A faction with its hazard level; classification/trash-filtered, sorted by hazard desc.</summary>
public record DashboardFactionHazard(
    string Name,
    string CaseNumber,
    string Href,
    HazardLevel Level);

/// <summary>One segment of a dashboard distribution: a category and its count.</summary>
public record DistributionSegment(string Designation, int Count);

/// <summary>The four dashboard distribution charts; classification-filtered, deterministic segment order for stable colours.</summary>
public record DashboardDistributions(
    IReadOnlyList<DistributionSegment> CasesByClassification,
    IReadOnlyList<DistributionSegment> MeasureOutcomes,
    IReadOnlyList<DistributionSegment> FactionsByHazard,
    IReadOnlyList<DistributionSegment> OpenRequestsByKind);

/// <summary>An activity-feed entry resolved from an audit row, rolled up to its parent record.</summary>
public record DashboardChange(
    DateTime Timestamp,
    string? AgentName,
    AuditAction Action,
    DashboardRecordType RecordType,
    string RecordId,
    string RecordName,
    string CaseNumber,
    /// <summary>Child kind for child-record changes; null otherwise.</summary>
    string? Detail,
    /// <summary>True when the record is in the trash (no detail link).</summary>
    bool RecordDeleted);
