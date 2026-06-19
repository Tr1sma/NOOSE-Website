using System.Security.Claims;
using NOOSE_Website.Data.Entities.Recruiting;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Recruiting;

namespace NOOSE_Website.Services;

/// <summary>Recruiting tests: HRB builds reusable tests, assigns one to an application, the applicant fills it, HRB reviews.</summary>
public interface IBewerbungTestService
{
    // ---- builder (HRB/leadership) ----
    Task<List<BewerbungTest>> GetTestsAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task<BewerbungTest> CreateTestAsync(string title, string? description, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task UpdateTestAsync(string id, string title, string? description, bool isActive, int? passPercent, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task DeleteTestAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task<TestEditModel?> GetEditModelAsync(string testId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task<BewerbungTestQuestion> AddQuestionAsync(string testId, TestQuestionType type, string prompt, bool required, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task UpdateQuestionAsync(string questionId, string prompt, bool required, int points, bool? correctYesNo, string? keywords, int? minKeywordHits, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task DeleteQuestionAsync(string questionId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task<BewerbungTestOption> AddOptionAsync(string questionId, string label, bool isCorrect, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task UpdateOptionAsync(string optionId, string label, bool isCorrect, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task DeleteOptionAsync(string optionId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    // ---- assignment + evaluation (HRB/leadership) ----
    Task<BewerbungTestAssignment?> GetAssignmentAsync(string bewerbungId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task AssignAsync(string bewerbungId, string testId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task<TestEvaluation?> GetEvaluationAsync(string bewerbungId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
    Task SetManualGradeAsync(string answerId, bool? manualCorrect, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    // ---- applicant ----
    Task<TestView?> GetAssignedForApplicantAsync(ClaimsPrincipal applicant, CancellationToken cancellationToken = default);
    Task SubmitAnswersAsync(string assignmentId, IReadOnlyList<TestAnswerInput> answers, ClaimsPrincipal applicant, CancellationToken cancellationToken = default);
}
