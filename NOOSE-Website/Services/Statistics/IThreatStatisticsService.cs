using NOOSE_Website.Models.Statistics;

namespace NOOSE_Website.Services.Statistics;

/// <summary>Threat-score analytics: how scores are distributed, how they moved, and what drives them.</summary>
public interface IThreatStatisticsService
{
    /// <summary>Score distribution of people and factions over fixed 10-point buckets.</summary>
    Task<ChartGrid> GetScoreHistogramAsync(StatisticsScope scope, CancellationToken cancellationToken = default);

    /// <summary>Hazard band by bucket, from the score history: shows the distribution shifting over time.</summary>
    Task<ChartMatrix> GetBandOverTimeAsync(StatisticsScope scope, string entityType,
        CancellationToken cancellationToken = default);

    /// <summary>Score-component profiles (S1-S4 factions, P1-P5 people) of the highest-scoring records.</summary>
    Task<ChartGrid> GetComponentProfilesAsync(StatisticsScope scope, string entityType, int topN = 3,
        CancellationToken cancellationToken = default);

    /// <summary>Score against confidence, to expose high scores resting on thin data.</summary>
    Task<IReadOnlyList<(string Name, IReadOnlyList<ChartPoint> Points)>> GetScoreVsConfidenceAsync(
        StatisticsScope scope, CancellationToken cancellationToken = default);

    /// <summary>Headline counts for the overview tiles.</summary>
    Task<ThreatHeadline> GetHeadlineAsync(StatisticsScope scope, CancellationToken cancellationToken = default);
}

/// <summary>Leading threat figures for the overview section.</summary>
public record ThreatHeadline(int ScoredRecords, int Elevated, int Critical, double AverageScore, double AverageConfidence);
