namespace NOOSE_Website.Models.Enums;

/// <summary>The slice of time a rule's threshold applies to.</summary>
public enum CounterIntelBucket
{
    /// <summary>The whole observation window as one bucket.</summary>
    Window = 0,

    /// <summary>Per calendar day.</summary>
    Day = 1,

    /// <summary>Per calendar hour.</summary>
    Hour = 2,

    /// <summary>Any span of SlidingMinutes, wherever it lands.</summary>
    Sliding = 3,
}

/// <summary>Display labels.</summary>
public static class CounterIntelBucketDisplay
{
    public static string Name(CounterIntelBucket bucket) => bucket switch
    {
        CounterIntelBucket.Window => "im gesamten Zeitraum",
        CounterIntelBucket.Day => "pro Tag",
        CounterIntelBucket.Hour => "pro Stunde",
        CounterIntelBucket.Sliding => "im gleitenden Fenster",
        _ => bucket.ToString(),
    };

    public static readonly IReadOnlyList<CounterIntelBucket> All =
    [
        CounterIntelBucket.Window,
        CounterIntelBucket.Day,
        CounterIntelBucket.Hour,
        CounterIntelBucket.Sliding,
    ];
}
