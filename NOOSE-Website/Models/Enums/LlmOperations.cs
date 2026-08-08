namespace NOOSE_Website.Models.Enums;

/// <summary>Why a turn stopped using tools. Distinguishes a ceiling that is set too low from a model that loops —
/// two problems with opposite fixes that look identical in a bare round count.</summary>
public enum LlmToolWithdrawal
{
    /// <summary>The model stopped asking on its own; nothing was withdrawn.</summary>
    Answered = 0,

    /// <summary>The round ceiling was reached.</summary>
    RoundsSpent = 1,

    /// <summary>The cost or token ceiling of the turn was reached.</summary>
    BudgetSpent = 2,

    /// <summary>A whole round was nothing but repeated calls.</summary>
    Looping = 3,

    /// <summary>The turn clock ran out while a tool was still reading; whatever text existed was handed over.</summary>
    TimeSpent = 4,
}

/// <summary>Class of failure, for counting. Coarse on purpose: the free-text detail stays in the error column,
/// and a panel needs buckets, not messages.</summary>
public enum LlmFailureKind
{
    Unknown = 0,

    /// <summary>The weekly quota was exhausted before the call went out.</summary>
    Quota = 1,

    /// <summary>The turn budget ran out.</summary>
    Timeout = 2,

    /// <summary>The agent stopped it.</summary>
    Cancelled = 3,

    /// <summary>The endpoint answered with an error, or not at all.</summary>
    Upstream = 4,

    /// <summary>The endpoint cannot serve this request shape (schema or tools).</summary>
    Capability = 5,

    /// <summary>Refused before the call: no use permission, or a read-only session.</summary>
    Denied = 6,
}

/// <summary>German labels of the two operations enums. Values are stored as ints, so the labels may change freely.</summary>
public static class LlmOperationsDisplay
{
    public static readonly LlmToolWithdrawal[] AllWithdrawals =
        [LlmToolWithdrawal.Answered, LlmToolWithdrawal.RoundsSpent, LlmToolWithdrawal.BudgetSpent,
         LlmToolWithdrawal.Looping, LlmToolWithdrawal.TimeSpent];

    public static readonly LlmFailureKind[] AllFailures =
        [LlmFailureKind.Quota, LlmFailureKind.Timeout, LlmFailureKind.Cancelled, LlmFailureKind.Upstream,
         LlmFailureKind.Capability, LlmFailureKind.Denied, LlmFailureKind.Unknown];

    public static string Name(LlmToolWithdrawal withdrawal) => withdrawal switch
    {
        LlmToolWithdrawal.Answered => "Von selbst geantwortet",
        LlmToolWithdrawal.RoundsSpent => "Runden aufgebraucht",
        LlmToolWithdrawal.BudgetSpent => "Budget aufgebraucht",
        LlmToolWithdrawal.Looping => "Wiederholungsschleife",
        LlmToolWithdrawal.TimeSpent => "Zeit abgelaufen",
        _ => withdrawal.ToString(),
    };

    public static string Name(LlmFailureKind kind) => kind switch
    {
        LlmFailureKind.Quota => "Kontingent erschöpft",
        LlmFailureKind.Timeout => "Zeitüberschreitung",
        LlmFailureKind.Cancelled => "Abgebrochen",
        LlmFailureKind.Upstream => "Endpunkt-Fehler",
        LlmFailureKind.Capability => "Anfrageform nicht unterstützt",
        LlmFailureKind.Denied => "Nicht zugelassen",
        LlmFailureKind.Unknown => "Unbekannt",
        _ => kind.ToString(),
    };
}
