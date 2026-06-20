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
public record TestView(
    string AssignmentId,
    string Title,
    string? Description,
    bool Completed,
    IReadOnlyList<TestQuestionView> Questions);

public record TestQuestionView(
    string QuestionId,
    TestQuestionType Type,
    string Prompt,
    bool Required,
    IReadOnlyList<TestOptionView> Options);

public record TestOptionView(string OptionId, string Label);

/// <summary>One applicant answer being submitted.</summary>
public class TestAnswerInput
{
    public string QuestionId { get; set; } = string.Empty;
    public string? SelectedOptionId { get; set; }
    public string? FreeText { get; set; }
}

/// <summary>HRB evaluation view of a completed test.</summary>
public record TestEvaluation(
    string Title,
    DateTime? CompletedAt,
    int TotalPoints,
    int MaxPoints,
    int? PassPercent,
    IReadOnlyList<TestEvaluationItem> Items)
{
    public int Percent => MaxPoints > 0 ? (int)Math.Round(100.0 * TotalPoints / MaxPoints) : 0;
    public bool? Passed => PassPercent is null ? null : Percent >= PassPercent.Value;
}

public record TestEvaluationItem(
    string? AnswerId,
    TestQuestionType Type,
    string Prompt,
    string? AnswerText,
    bool? AutoCorrect,
    bool? ManualCorrect,
    bool? EffectiveCorrect,
    int Points,
    int AwardedPoints,
    string? CorrectAnswer,
    IReadOnlyList<string> MatchedKeywords,
    IReadOnlyList<string> MissedKeywords);

/// <summary>HRB test-builder model: a test with its questions and their options.</summary>
public record TestEditModel(BewerbungTest Test, IReadOnlyList<TestQuestionEdit> Questions);

public record TestQuestionEdit(BewerbungTestQuestion Question, IReadOnlyList<BewerbungTestOption> Options);
