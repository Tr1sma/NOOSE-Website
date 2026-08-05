using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Statistics;

/// <summary>One named series of values aligned to a shared label axis.</summary>
public record ChartSeriesData(string Name, IReadOnlyList<double> Values);

/// <summary>A label axis plus its series; the shape every category, trend and radar chart consumes.</summary>
public record ChartGrid(IReadOnlyList<string> Labels, IReadOnlyList<ChartSeriesData> Series)
{
    /// <summary>An axis with no series at all.</summary>
    public static ChartGrid Empty { get; } = new([], []);

    /// <summary>True when there is nothing to draw — charts must show a hint instead of NaN geometry.</summary>
    public bool IsEmpty => Labels.Count == 0 || Series.Count == 0 || Series.All(s => s.Values.All(v => v == 0));
}

/// <summary>Row by column counts for a heatmap; <paramref name="Max"/> carries the ramp domain.</summary>
public record ChartMatrix(
    IReadOnlyList<string> Rows,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<int>> Cells,
    int Max)
{
    /// <summary>An empty matrix.</summary>
    public static ChartMatrix Empty { get; } = new([], [], [], 0);

    /// <summary>True when no cell carries a value.</summary>
    public bool IsEmpty => Rows.Count == 0 || Columns.Count == 0 || Max <= 0;
}

/// <summary>One histogram bucket over the half-open interval [From, Until).</summary>
public record ChartBucket(string Label, double From, double Until, int Count);

/// <summary>A day-resolution count, for the calendar heatmap.</summary>
public record ChartDay(DateOnly Day, int Count);

/// <summary>A directed weighted edge for a Sankey flow.</summary>
public record ChartFlow(string From, string To, int Weight);

/// <summary>A ranked row with an optional drill-down target.</summary>
public record ChartRank(string Name, string? Caption, double Value, string? Href);

/// <summary>A single point for a correlation scatter.</summary>
public record ChartPoint(double X, double Y, string Label, string? Href);

/// <summary>A before/after pair for one record, rendered as a dumbbell.</summary>
public record ChartMove(string Name, string? Caption, double From, double To, string? Href);

/// <summary>A part-of-limit reading for a meter: how many of <paramref name="Total"/> are in the good state.</summary>
public record ChartRatio(string Label, int Value, int Total, string? Href)
{
    /// <summary>Share in [0, 1]; zero when nothing is measured.</summary>
    public double Share => Total <= 0 ? 0 : (double)Value / Total;
}

/// <summary>A treemap leaf: area from <paramref name="Weight"/>, colour from <paramref name="Level"/>.</summary>
public record ChartTile(string Name, int Weight, HazardLevel Level, string? Href);

/// <summary>Time window presets offered by the statistics filter bar.</summary>
/// <remarks>
/// Deliberately a closed set rather than a free range: the memory-cache key carries the window, so a
/// free range would blow up the key space.
/// </remarks>
public enum StatisticsRange
{
    /// <summary>Last 30 days, bucketed per day.</summary>
    Days30 = 0,

    /// <summary>Last 90 days, bucketed per week.</summary>
    Days90 = 1,

    /// <summary>Last 12 months, bucketed per month.</summary>
    Months12 = 2,

    /// <summary>Last 24 months, bucketed per month.</summary>
    Months24 = 3,
}

/// <summary>Bucket width a range is aggregated at.</summary>
public enum StatisticsGranularity
{
    /// <summary>One bucket per calendar day.</summary>
    Day = 0,

    /// <summary>One bucket per calendar week.</summary>
    Week = 1,

    /// <summary>One bucket per calendar month.</summary>
    Month = 2,
}

/// <summary>Labels, URL tokens and derived bucket width for <see cref="StatisticsRange"/>.</summary>
public static class StatisticsRangeDisplay
{
    /// <summary>All ranges in the order the filter bar shows them.</summary>
    public static readonly IReadOnlyList<StatisticsRange> All =
    [
        StatisticsRange.Days30, StatisticsRange.Days90, StatisticsRange.Months12, StatisticsRange.Months24,
    ];

    /// <summary>German label for the filter bar.</summary>
    public static string Name(StatisticsRange range) => range switch
    {
        StatisticsRange.Days30 => "30 Tage",
        StatisticsRange.Days90 => "90 Tage",
        StatisticsRange.Months12 => "12 Monate",
        StatisticsRange.Months24 => "24 Monate",
        _ => "12 Monate",
    };

    /// <summary>Short token used in the URL and in cache keys.</summary>
    public static string Token(StatisticsRange range) => range switch
    {
        StatisticsRange.Days30 => "30d",
        StatisticsRange.Days90 => "90d",
        StatisticsRange.Months12 => "12m",
        StatisticsRange.Months24 => "24m",
        _ => "12m",
    };

    /// <summary>Range for a URL token; falls back to the default when unknown.</summary>
    public static StatisticsRange Parse(string? token) => token switch
    {
        "30d" => StatisticsRange.Days30,
        "90d" => StatisticsRange.Days90,
        "24m" => StatisticsRange.Months24,
        _ => StatisticsRange.Months12,
    };

    /// <summary>Bucket width; derived from the range so the filter bar needs no second control.</summary>
    public static StatisticsGranularity Granularity(StatisticsRange range) => range switch
    {
        StatisticsRange.Days30 => StatisticsGranularity.Day,
        StatisticsRange.Days90 => StatisticsGranularity.Week,
        _ => StatisticsGranularity.Month,
    };

    /// <summary>Number of buckets the range spans.</summary>
    public static int BucketCount(StatisticsRange range) => range switch
    {
        StatisticsRange.Days30 => 30,
        StatisticsRange.Days90 => 13,
        StatisticsRange.Months12 => 12,
        StatisticsRange.Months24 => 24,
        _ => 12,
    };

    /// <summary>Inclusive UTC start of the window, aligned to the bucket width.</summary>
    public static DateTime StartUtc(StatisticsRange range, DateTime nowUtc) => range switch
    {
        StatisticsRange.Days30 => nowUtc.Date.AddDays(-(BucketCount(range) - 1)),
        StatisticsRange.Days90 => StartOfWeek(nowUtc.Date).AddDays(-7 * (BucketCount(range) - 1)),
        _ => new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddMonths(-(BucketCount(range) - 1)),
    };

    /// <summary>Monday of the week the date falls in.</summary>
    public static DateTime StartOfWeek(DateTime date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7;
        return DateTime.SpecifyKind(date.Date.AddDays(-offset), DateTimeKind.Utc);
    }
}

/// <summary>Bucket maths for a window; shared by the statistics services and the panels that plot them.</summary>
/// <remarks>
/// EF cannot translate a UTC-to-Europe/Berlin conversion, so every query bounds its window in SQL and
/// hands back bare timestamps that are bucketed here.
/// </remarks>
public static class StatisticsBuckets
{
    /// <summary>Inclusive bucket start instants covering the scope's window.</summary>
    public static List<DateTime> Starts(StatisticsScope scope, DateTime nowUtc)
    {
        var start = scope.StartUtc(nowUtc);
        var list = new List<DateTime>(scope.BucketCount);
        for (var i = 0; i < scope.BucketCount; i++)
        {
            list.Add(scope.Granularity switch
            {
                StatisticsGranularity.Day => start.AddDays(i),
                StatisticsGranularity.Week => start.AddDays(7 * i),
                _ => start.AddMonths(i),
            });
        }
        return list;
    }

    /// <summary>Counts timestamps into the buckets; anything before the first bucket is dropped.</summary>
    public static IReadOnlyList<double> Count(IReadOnlyList<DateTime> timestamps, List<DateTime> buckets)
    {
        var counts = new double[buckets.Count];
        foreach (var timestamp in timestamps)
        {
            var index = IndexOf(buckets, timestamp);
            if (index >= 0)
            {
                counts[index]++;
            }
        }
        return counts;
    }

    /// <summary>Bucket a timestamp falls in, or -1 when it predates the window.</summary>
    public static int IndexOf(List<DateTime> buckets, DateTime timestamp)
    {
        for (var i = buckets.Count - 1; i >= 0; i--)
        {
            if (timestamp >= buckets[i])
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>German axis label for a bucket start.</summary>
    public static string Label(DateTime start, StatisticsScope scope)
    {
        var de = System.Globalization.CultureInfo.GetCultureInfo("de-DE");
        return scope.Granularity switch
        {
            StatisticsGranularity.Day => start.ToString("dd.MM.", de),
            StatisticsGranularity.Week => start.ToString("dd.MM.", de),
            _ => start.ToString("MMM yy", de),
        };
    }
}

/// <summary>What the current viewer may see plus the window they selected; the parameter every chart service takes.</summary>
/// <remarks>
/// <paramref name="IncludeClassified"/> is already the AND of "may read classified" and the filter-bar
/// toggle, so a service never has to consult the principal again. It is also the only visibility axis in
/// the cache key, which keeps that key at two values.
/// </remarks>
public record StatisticsScope(bool IncludeClassified, StatisticsRange Range)
{
    /// <summary>Cache-key fragment: the visibility axis plus the window.</summary>
    public string CacheToken => $"{(IncludeClassified ? "c" : "n")}:{StatisticsRangeDisplay.Token(Range)}";

    /// <summary>Bucket width derived from the window.</summary>
    public StatisticsGranularity Granularity => StatisticsRangeDisplay.Granularity(Range);

    /// <summary>Number of buckets in the window.</summary>
    public int BucketCount => StatisticsRangeDisplay.BucketCount(Range);

    /// <summary>Inclusive UTC start of the window.</summary>
    public DateTime StartUtc(DateTime nowUtc) => StatisticsRangeDisplay.StartUtc(Range, nowUtc);
}
