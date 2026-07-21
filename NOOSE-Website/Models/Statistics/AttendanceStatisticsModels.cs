using NOOSE_Website.Models.Dashboard;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Statistics;

/// <summary>One agent's attendance record over the evaluation window.</summary>
public record AttendanceAgentRow(
    string AgentId,
    string Codename,
    string Href,
    int Evaluated,
    int Present,
    int Excused,
    int Missing,
    AttendanceAnomalyLevel Level);

/// <summary>Leadership report over absences and meeting attendance.</summary>
public record AttendanceReport(
    int WindowSize,
    int YellowThreshold,
    int RedThreshold,
    int MeetingsEvaluated,
    int AbsencesOpenAcknowledgement,
    IReadOnlyList<DistributionSegment> AttendanceDistribution,
    IReadOnlyList<DistributionSegment> AbsencesByCategory,
    IReadOnlyList<StatisticsMonth> TimeSeries,
    IReadOnlyList<int> AbsenceCountsPerMonth,
    IReadOnlyList<int> MissingCountsPerMonth,
    IReadOnlyList<AttendanceAgentRow> Anomalies,
    IReadOnlyList<AttendanceAgentRow> AllAgents);

/// <summary>Anomaly classification; the thresholds come from system settings.</summary>
public static class AttendanceAnomalyLogic
{
    /// <summary>Clamps the three numbers into a coherent window >= red >= yellow >= 1.</summary>
    public static (int Window, int Yellow, int Red) Coherent(int window, int yellow, int red)
    {
        yellow = Math.Clamp(yellow, 1, 50);
        red = Math.Clamp(red, yellow, 50);
        window = Math.Clamp(window, red, 50);
        return (window, yellow, red);
    }

    // below a full window no honest judgement is possible, so never flag
    public static AttendanceAnomalyLevel From(int evaluated, int missing, int window, int yellow, int red)
        => evaluated < window ? AttendanceAnomalyLevel.Insufficient
            : missing >= red ? AttendanceAnomalyLevel.Red
            : missing >= yellow ? AttendanceAnomalyLevel.Yellow
            : AttendanceAnomalyLevel.None;
}
