using MudBlazor;

namespace NOOSE_Website.Models.Enums;

/// <summary>Where a feedback report stands in the leadership workflow.</summary>
public enum FeedbackStatus
{
    /// <summary>Filed, nobody looked at it yet.</summary>
    New = 0,
    /// <summary>Leadership agrees and scheduled it.</summary>
    Accepted = 1,
    /// <summary>Currently being built.</summary>
    InProgress = 2,
    /// <summary>Shipped.</summary>
    Done = 3,
    /// <summary>Will not happen.</summary>
    Rejected = 4,
    /// <summary>Parked for later without a date.</summary>
    Deferred = 5,
}

/// <summary>Display labels, icons and chip colours per feedback stage.</summary>
public static class FeedbackStatusDisplay
{
    public static string Name(FeedbackStatus status) => status switch
    {
        FeedbackStatus.New => "Neu",
        FeedbackStatus.Accepted => "Angenommen",
        FeedbackStatus.InProgress => "In Umsetzung",
        FeedbackStatus.Done => "Umgesetzt",
        FeedbackStatus.Rejected => "Abgelehnt",
        FeedbackStatus.Deferred => "Zurückgestellt",
        _ => "—",
    };

    public static string Icon(FeedbackStatus status) => status switch
    {
        FeedbackStatus.New => Icons.Material.Filled.HourglassEmpty,
        FeedbackStatus.Accepted => Icons.Material.Filled.ThumbUp,
        FeedbackStatus.InProgress => Icons.Material.Filled.Autorenew,
        FeedbackStatus.Done => Icons.Material.Filled.CheckCircle,
        FeedbackStatus.Rejected => Icons.Material.Filled.Cancel,
        FeedbackStatus.Deferred => Icons.Material.Filled.Schedule,
        _ => Icons.Material.Filled.Feedback,
    };

    public static Color ChipColor(FeedbackStatus status) => status switch
    {
        FeedbackStatus.New => Color.Info,
        FeedbackStatus.Accepted => Color.Primary,
        FeedbackStatus.InProgress => Color.Warning,
        FeedbackStatus.Done => Color.Success,
        FeedbackStatus.Rejected => Color.Error,
        FeedbackStatus.Deferred => Color.Default,
        _ => Color.Default,
    };

    public static readonly IReadOnlyList<FeedbackStatus> All = new[]
    {
        FeedbackStatus.New,
        FeedbackStatus.Accepted,
        FeedbackStatus.InProgress,
        FeedbackStatus.Done,
        FeedbackStatus.Rejected,
        FeedbackStatus.Deferred,
    };
}
