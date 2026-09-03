using MudBlazor;

namespace NOOSE_Website.Models.Enums;

/// <summary>How far an applicant is through their assigned test.</summary>
public enum TestAttemptState
{
    NotStarted = 0,
    Running = 1,
    Expired = 2,
    Submitted = 3,
}

/// <summary>Display labels and chip colors.</summary>
public static class TestAttemptStateDisplay
{
    public static string Name(TestAttemptState state) => state switch
    {
        TestAttemptState.NotStarted => "Nicht begonnen",
        TestAttemptState.Running => "Läuft",
        TestAttemptState.Expired => "Zeit abgelaufen",
        TestAttemptState.Submitted => "Abgegeben",
        _ => "—",
    };

    /// <summary>Chip colour; expired is a warning, never Error.</summary>
    /// <remarks>Error is already the failed-verdict colour in the grading panel, so a red expiry chip
    /// would read as a second verdict next to the first.</remarks>
    public static Color ChipColor(TestAttemptState state) => state switch
    {
        TestAttemptState.NotStarted => Color.Default,
        TestAttemptState.Running => Color.Info,
        TestAttemptState.Expired => Color.Warning,
        TestAttemptState.Submitted => Color.Success,
        _ => Color.Default,
    };
}

/// <summary>Attempt state from the stored stamps; the deadline decides in the gap before finalisation.</summary>
/// <remarks>Four surfaces need this same classification, and without bUnit a static helper is the only
/// place the branching can be tested at all.</remarks>
public static class TestAttemptLogic
{
    /// <summary>Effective state; a deadline already past counts as expired even before anything closed it.</summary>
    public static TestAttemptState State(DateTime? startedAt, DateTime? deadlineAt, DateTime? completedAt,
        bool timedOut, DateTime now)
        => completedAt is not null
            ? (timedOut ? TestAttemptState.Expired : TestAttemptState.Submitted)
            : startedAt is null
                ? TestAttemptState.NotStarted
                : deadlineAt is { } due && due <= now
                    ? TestAttemptState.Expired
                    : TestAttemptState.Running;

    /// <summary>Minutes left, rounded up; null without a limit or once it ran out.</summary>
    public static int? RemainingMinutes(DateTime? deadlineAt, DateTime now)
        => deadlineAt is { } due && due > now ? (int)Math.Ceiling((due - now).TotalMinutes) : null;
}
