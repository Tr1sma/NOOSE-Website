using MudBlazor;

namespace NOOSE_Website.Models.Enums;

/// <summary>What kind of feedback an agent reports about the site.</summary>
public enum FeedbackKind
{
    /// <summary>Suggestion for improvement.</summary>
    Improvement = 0,
    /// <summary>Something is broken.</summary>
    Bug = 1,
    /// <summary>Complaint about a process or a feature.</summary>
    Complaint = 2,
    /// <summary>Wish for a new feature.</summary>
    FeatureRequest = 3,
}

/// <summary>Display labels and icons.</summary>
public static class FeedbackKindDisplay
{
    public static string Name(FeedbackKind kind) => kind switch
    {
        FeedbackKind.Improvement => "Verbesserungsvorschlag",
        FeedbackKind.Bug => "Bug-Meldung",
        FeedbackKind.Complaint => "Mängelmeldung",
        FeedbackKind.FeatureRequest => "Feature-Request",
        _ => "—",
    };

    public static string Icon(FeedbackKind kind) => kind switch
    {
        FeedbackKind.Improvement => Icons.Material.Filled.Lightbulb,
        FeedbackKind.Bug => Icons.Material.Filled.BugReport,
        FeedbackKind.Complaint => Icons.Material.Filled.SentimentDissatisfied,
        FeedbackKind.FeatureRequest => Icons.Material.Filled.AutoAwesome,
        _ => Icons.Material.Filled.HelpOutline,
    };

    public static readonly IReadOnlyList<FeedbackKind> All = new[]
    {
        FeedbackKind.Improvement,
        FeedbackKind.Bug,
        FeedbackKind.Complaint,
        FeedbackKind.FeatureRequest,
    };
}
