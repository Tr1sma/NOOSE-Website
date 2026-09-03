using NOOSE_Website.Data.Entities.Recruiting;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Models.Recruiting;

/// <summary>Applicant form input for a new application.</summary>
public class BewerbungSubmitModel
{
    public string? AcademicDegree { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime? BirthDate { get; set; }
    public string? Employer { get; set; }
    public string? PriorExperience { get; set; }
    public string? CoverLetter { get; set; }
}

/// <summary>Info about an application's linked Person file (threat score), for the HRB panel.</summary>
public record LinkedPersonInfo(
    string PersonId,
    string Name,
    string CaseNumber,
    int? ThreatScore,
    int? ThreatConfidence,
    DateTime? ScoreCalculatedAt,
    bool IsClassified);

/// <summary>An active recruitment ban or blacklist entry, for the HRB panel and the applicant gate.</summary>
public record BewerbungssperreInfo(
    string Id,
    string AgentId,
    string? DiscordId,
    string? ApplicantName,
    string? BewerbungId,
    bool IsBlacklist,
    DateTime? BannedUntil,
    string? Reason,
    DateTime CreatedAt,
    string? CreatedByName);

/// <summary>An applicant-facing view of an assigned test.</summary>
/// <remarks>Deliberately carries no points, no answer key and no verdict: the applicant must not learn
/// the outcome. CaseNumber is here only to watermark the questionnaire.</remarks>
public record TestView(
    string AssignmentId,
    string CaseNumber,
    string Title,
    string? Description,
    bool Completed,
    IReadOnlyList<TestQuestionView> Questions);

/// <param name="AllowMultiple">Shape of the form control, not a hint at the key: it is the author's own switch.</param>
public record TestQuestionView(
    string QuestionId,
    TestQuestionType Type,
    string Prompt,
    bool Required,
    bool AllowMultiple,
    IReadOnlyList<TestOptionView> Options);

public record TestOptionView(string OptionId, string Label);

/// <summary>One applicant answer being submitted.</summary>
public class TestAnswerInput
{
    public string QuestionId { get; set; } = string.Empty;

    /// <summary>Every ticked option; a single-choice question sends exactly one.</summary>
    public IReadOnlyList<string> SelectedOptionIds { get; set; } = [];

    public string? FreeText { get; set; }
}

/// <summary>HRB evaluation view of a completed test.</summary>
/// <param name="GradedAt">Set once the grading was declared finished; the totals are then the frozen ones.</param>
public record TestEvaluation(
    string Title,
    DateTime? CompletedAt,
    int TotalPoints,
    int MaxPoints,
    int? PassPercent,
    DateTime? GradedAt,
    string? GradedByName,
    IReadOnlyList<TestEvaluationItem> Items)
{
    public int Percent => MaxPoints > 0 ? (int)Math.Round(100.0 * TotalPoints / MaxPoints) : 0;
    public bool? Passed => PassPercent is null ? null : Percent >= PassPercent.Value;

    /// <summary>Questions neither the machine nor the grader has decided; grading cannot be closed while any remain.</summary>
    public int OpenCount => Items.Count(i => !i.IsGraded);
}

/// <param name="QuestionId">The grade is keyed on the question, not on an answer row: a question added after the
/// applicant submitted has no row and would otherwise count against the maximum without ever being gradeable.</param>
public record TestEvaluationItem(
    string? AnswerId,
    string QuestionId,
    TestQuestionType Type,
    string Prompt,
    string? AnswerText,
    bool? AutoCorrect,
    bool? ManualCorrect,
    int? ManualPoints,
    bool? EffectiveCorrect,
    int Points,
    int AwardedPoints,
    string? CorrectAnswer,
    IReadOnlyList<string> MatchedKeywords,
    IReadOnlyList<string> MissedKeywords)
{
    /// <summary>Decided by the machine or by hand; an undecidable question stays open until someone awards points.</summary>
    public bool IsGraded => ManualPoints is not null || EffectiveCorrect is not null;
}

/// <summary>HRB test-builder model: a test with its questions and their options.</summary>
public record TestEditModel(BewerbungTest Test, IReadOnlyList<TestQuestionEdit> Questions);

public record TestQuestionEdit(BewerbungTestQuestion Question, IReadOnlyList<BewerbungTestOption> Options);
