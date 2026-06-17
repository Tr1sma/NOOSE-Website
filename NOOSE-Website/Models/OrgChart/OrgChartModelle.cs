using NOOSE_Website.Data.Entities;
using NOOSE_Website.Data.Entities.Taskforces;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.OrgChart;

/// <summary>Read-only org-chart data: rank levels, TRU/HRB cross-section, and visible approved taskforces.</summary>
public sealed record OrgChartData(
    IReadOnlyList<RankGroup> Ranks,
    IReadOnlyList<Agent> Tru,
    IReadOnlyList<Agent> Hrb,
    IReadOnlyList<TaskforceStaffing> Taskforces);

/// <summary>All active agents of one rank.</summary>
public sealed record RankGroup(Rank Rank, IReadOnlyList<Agent> Agents);

/// <summary>A taskforce with its visible staffing; lead first.</summary>
public sealed record TaskforceStaffing(Taskforce Taskforce, IReadOnlyList<TaskforceAgent> Members);
