namespace NOOSE_Website.Models.Enums;

/// <summary>What a rule counts inside one bucket.</summary>
public enum CounterIntelCountMode
{
    /// <summary>Every matching event, repeats included.</summary>
    Events = 0,

    /// <summary>Distinct records touched, repeats collapsed.</summary>
    DistinctRecords = 1,
}

/// <summary>Display labels.</summary>
public static class CounterIntelCountModeDisplay
{
    public static string Name(CounterIntelCountMode mode) => mode switch
    {
        CounterIntelCountMode.Events => "Ereignisse",
        CounterIntelCountMode.DistinctRecords => "verschiedene Akten",
        _ => mode.ToString(),
    };

    public static readonly IReadOnlyList<CounterIntelCountMode> All =
    [
        CounterIntelCountMode.Events,
        CounterIntelCountMode.DistinctRecords,
    ];
}
