using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Feedback;

/// <summary>Form model for filing feedback.</summary>
public class FeedbackInput
{
    public FeedbackKind Kind { get; set; } = FeedbackKind.Improvement;

    /// <summary>Route the feedback was filed from.</summary>
    public string? PageRoute { get; set; }

    /// <summary>Active tab on the page, if any.</summary>
    public string? PageTab { get; set; }

    public string Text { get; set; } = string.Empty;
}

/// <summary>One feedback entry as shown in a list.</summary>
public record FeedbackRow(
    string Id,
    FeedbackKind Kind,
    FeedbackStatus Status,
    string? PageRoute,
    string? PageTab,
    string Text,
    string AgentId,
    string AgentCodename,
    DateTime CreatedAt,
    string? Response,
    string? DeciderName,
    DateTime? DecidedAt);
