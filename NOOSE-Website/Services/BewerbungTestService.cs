using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Authorization;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Recruiting;
using NOOSE_Website.Infrastructure.Chat;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Recruiting;

namespace NOOSE_Website.Services;

/// <inheritdoc cref="IBewerbungTestService" />
public class BewerbungTestService(
    IDbContextFactory<AppDbContext> dbFactory,
    BewerbungBroadcaster broadcaster,
    INotificationService notifications) : IBewerbungTestService
{
    public async Task<List<BewerbungTest>> GetTestsAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadership(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.BewerbungTests.AsNoTracking()
            .OrderBy(t => t.Sorting).ThenBy(t => t.Title)
            .ToListAsync(cancellationToken);
    }

    public async Task<BewerbungTest> CreateTestAsync(string title, string? description, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadership(actor);
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new InvalidOperationException("Der Titel darf nicht leer sein.");
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var max = await db.BewerbungTests.MaxAsync(t => (int?)t.Sorting, cancellationToken) ?? 0;
        var test = new BewerbungTest { Title = title.Trim(), Description = Trim(description), Sorting = max + 1 };
        db.BewerbungTests.Add(test);
        await db.SaveChangesAsync(cancellationToken);
        return test;
    }

    public async Task UpdateTestAsync(string id, string title, string? description, bool isActive, int? passPercent, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadership(actor);
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new InvalidOperationException("Der Titel darf nicht leer sein.");
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var test = await db.BewerbungTests.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Test nicht gefunden.");
        test.Title = title.Trim();
        test.Description = Trim(description);
        test.IsActive = isActive;
        test.PassPercent = passPercent is null ? null : Math.Clamp(passPercent.Value, 0, 100);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteTestAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadership(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var test = await db.BewerbungTests.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (test is null)
        {
            return;
        }
        db.BewerbungTests.Remove(test); // soft-delete
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<TestEditModel?> GetEditModelAsync(string testId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadership(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var test = await db.BewerbungTests.AsNoTracking().FirstOrDefaultAsync(t => t.Id == testId, cancellationToken);
        if (test is null)
        {
            return null;
        }
        var questions = await db.BewerbungTestQuestions.AsNoTracking()
            .Where(q => q.TestId == testId).OrderBy(q => q.Sorting).ToListAsync(cancellationToken);
        var questionIds = questions.Select(q => q.Id).ToList();
        var options = await db.BewerbungTestOptions.AsNoTracking()
            .Where(o => questionIds.Contains(o.QuestionId)).OrderBy(o => o.Sorting).ToListAsync(cancellationToken);

        var edits = questions
            .Select(q => new TestQuestionEdit(q, options.Where(o => o.QuestionId == q.Id).ToList()))
            .ToList();
        return new TestEditModel(test, edits);
    }

    public async Task<BewerbungTestQuestion> AddQuestionAsync(string testId, TestQuestionType type, string prompt, bool required, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadership(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var max = await db.BewerbungTestQuestions.Where(q => q.TestId == testId)
            .MaxAsync(q => (int?)q.Sorting, cancellationToken) ?? 0;
        var question = new BewerbungTestQuestion
        {
            TestId = testId,
            Type = type,
            Prompt = (prompt ?? string.Empty).Trim(),
            Required = required,
            Sorting = max + 1,
        };
        db.BewerbungTestQuestions.Add(question);
        await db.SaveChangesAsync(cancellationToken);
        return question;
    }

    public async Task UpdateQuestionAsync(string questionId, string prompt, bool required, int points, bool? correctYesNo, string? keywords, int? minKeywordHits, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadership(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var question = await db.BewerbungTestQuestions.FirstOrDefaultAsync(q => q.Id == questionId, cancellationToken)
            ?? throw new InvalidOperationException("Frage nicht gefunden.");
        question.Prompt = (prompt ?? string.Empty).Trim();
        question.Required = required;
        question.Points = Math.Max(0, points);
        question.CorrectYesNo = question.Type == TestQuestionType.YesNo ? correctYesNo : null;
        question.Keywords = question.Type == TestQuestionType.FreeText ? Trim(keywords) : null;
        question.MinKeywordHits = question.Type == TestQuestionType.FreeText && minKeywordHits is > 0 ? minKeywordHits : null;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteQuestionAsync(string questionId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadership(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var question = await db.BewerbungTestQuestions.FirstOrDefaultAsync(q => q.Id == questionId, cancellationToken);
        if (question is null)
        {
            return;
        }
        db.BewerbungTestQuestions.Remove(question);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<BewerbungTestOption> AddOptionAsync(string questionId, string label, bool isCorrect, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadership(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var max = await db.BewerbungTestOptions.Where(o => o.QuestionId == questionId)
            .MaxAsync(o => (int?)o.Sorting, cancellationToken) ?? 0;
        var option = new BewerbungTestOption
        {
            QuestionId = questionId,
            Label = (label ?? string.Empty).Trim(),
            IsCorrect = isCorrect,
            Sorting = max + 1,
        };
        db.BewerbungTestOptions.Add(option);
        await db.SaveChangesAsync(cancellationToken);
        return option;
    }

    public async Task UpdateOptionAsync(string optionId, string label, bool isCorrect, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadership(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var option = await db.BewerbungTestOptions.FirstOrDefaultAsync(o => o.Id == optionId, cancellationToken)
            ?? throw new InvalidOperationException("Antwortoption nicht gefunden.");
        option.Label = (label ?? string.Empty).Trim();
        option.IsCorrect = isCorrect;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteOptionAsync(string optionId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadership(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var option = await db.BewerbungTestOptions.FirstOrDefaultAsync(o => o.Id == optionId, cancellationToken);
        if (option is null)
        {
            return;
        }
        db.BewerbungTestOptions.Remove(option);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<BewerbungTestAssignment?> GetAssignmentAsync(string bewerbungId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadership(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.BewerbungTestAssignments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.BewerbungId == bewerbungId, cancellationToken);
    }

    public async Task AssignAsync(string bewerbungId, string testId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadership(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var bewerbung = await db.Bewerbungen.FirstOrDefaultAsync(b => b.Id == bewerbungId, cancellationToken)
            ?? throw new InvalidOperationException("Bewerbung nicht gefunden.");
        if (await db.BewerbungTestAssignments.AnyAsync(a => a.BewerbungId == bewerbungId, cancellationToken))
        {
            throw new InvalidOperationException("Dieser Bewerbung ist bereits ein Test zugewiesen.");
        }
        if (!await db.BewerbungTests.AnyAsync(t => t.Id == testId, cancellationToken))
        {
            throw new InvalidOperationException("Test nicht gefunden.");
        }

        db.BewerbungTestAssignments.Add(new BewerbungTestAssignment
        {
            BewerbungId = bewerbungId,
            TestId = testId,
            AssignedByName = actor.GetCodename(),
        });

        // entering the test phase
        if (bewerbung.Status is BewerbungStatus.Eingereicht or BewerbungStatus.InSicherheitspruefung)
        {
            bewerbung.Status = BewerbungStatus.ImTest;
        }
        await db.SaveChangesAsync(cancellationToken);
        broadcaster.Report(bewerbungId);

        try
        {
            await notifications.NotifyAsync(bewerbung.ApplicantUserId, NotificationType.Recruiting,
                "Dir wurde ein Test zugewiesen.", "/portal/test", cancellationToken);
        }
        catch { /* best effort */ }
    }

    public async Task<TestEvaluation?> GetEvaluationAsync(string bewerbungId, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadership(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var assignment = await db.BewerbungTestAssignments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.BewerbungId == bewerbungId, cancellationToken);
        if (assignment is null)
        {
            return null;
        }
        var test = await db.BewerbungTests.AsNoTracking().FirstOrDefaultAsync(t => t.Id == assignment.TestId, cancellationToken);
        var questions = await db.BewerbungTestQuestions.AsNoTracking()
            .Where(q => q.TestId == assignment.TestId).OrderBy(q => q.Sorting).ToListAsync(cancellationToken);
        var questionIds = questions.Select(q => q.Id).ToList();
        var options = await db.BewerbungTestOptions.AsNoTracking()
            .Where(o => questionIds.Contains(o.QuestionId)).ToListAsync(cancellationToken);
        var answers = await db.BewerbungTestAnswers.AsNoTracking()
            .Where(a => a.AssignmentId == assignment.Id).ToListAsync(cancellationToken);

        var items = new List<TestEvaluationItem>();
        var total = 0;
        var max = 0;
        foreach (var q in questions)
        {
            max += q.Points;
            var answer = answers.FirstOrDefault(a => a.QuestionId == q.Id);
            string? answerText = null;
            bool? autoCorrect = null;
            string? correctAnswer = null;
            IReadOnlyList<string> matched = Array.Empty<string>();
            IReadOnlyList<string> missed = Array.Empty<string>();

            switch (q.Type)
            {
                case TestQuestionType.MultipleChoice:
                    var chosen = options.FirstOrDefault(o => o.Id == answer?.SelectedOptionId);
                    answerText = chosen?.Label;
                    autoCorrect = TestGrading.GradeMultipleChoice(chosen);
                    var correct = options.Where(o => o.QuestionId == q.Id && o.IsCorrect).Select(o => o.Label).ToList();
                    correctAnswer = correct.Count > 0 ? string.Join(", ", correct) : null;
                    break;

                case TestQuestionType.YesNo:
                    answerText = answer?.FreeTextAnswer;
                    autoCorrect = TestGrading.GradeYesNo(answerText, q.CorrectYesNo);
                    correctAnswer = q.CorrectYesNo is null ? null : (q.CorrectYesNo.Value ? "Ja" : "Nein");
                    break;

                default: // FreeText
                    answerText = answer?.FreeTextAnswer;
                    (autoCorrect, matched, missed) = TestGrading.GradeFreeText(answerText, q.Keywords, q.MinKeywordHits);
                    break;
            }

            var effective = answer?.ManualCorrect ?? autoCorrect;
            var awarded = effective == true ? q.Points : 0;
            total += awarded;

            items.Add(new TestEvaluationItem(
                answer?.Id, q.Type, q.Prompt, answerText,
                autoCorrect, answer?.ManualCorrect, effective,
                q.Points, awarded, correctAnswer, matched, missed));
        }

        return new TestEvaluation(test?.Title ?? "Test", assignment.CompletedAt, total, max, test?.PassPercent, items);
    }

    public async Task SetManualGradeAsync(string answerId, bool? manualCorrect, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadership(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var answer = await db.BewerbungTestAnswers.FirstOrDefaultAsync(a => a.Id == answerId, cancellationToken)
            ?? throw new InvalidOperationException("Antwort nicht gefunden.");
        answer.ManualCorrect = manualCorrect;
        await db.SaveChangesAsync(cancellationToken);

        var bewerbungId = await db.BewerbungTestAssignments.AsNoTracking()
            .Where(a => a.Id == answer.AssignmentId).Select(a => a.BewerbungId).FirstOrDefaultAsync(cancellationToken);
        if (!string.IsNullOrEmpty(bewerbungId))
        {
            broadcaster.Report(bewerbungId);
        }
    }

    public async Task<TestView?> GetAssignedForApplicantAsync(ClaimsPrincipal applicant, CancellationToken cancellationToken = default)
    {
        Permission.RequireApplicant(applicant);
        var userId = applicant.GetAgentId();
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var bewerbungId = await db.Bewerbungen.AsNoTracking()
            .Where(b => b.ApplicantUserId == userId)
            .OrderByDescending(b => b.SubmittedAt)
            .Select(b => b.Id).FirstOrDefaultAsync(cancellationToken);
        if (bewerbungId is null)
        {
            return null;
        }
        var assignment = await db.BewerbungTestAssignments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.BewerbungId == bewerbungId, cancellationToken);
        if (assignment is null)
        {
            return null;
        }
        var test = await db.BewerbungTests.AsNoTracking().FirstOrDefaultAsync(t => t.Id == assignment.TestId, cancellationToken);
        if (test is null)
        {
            return null;
        }
        var questions = await db.BewerbungTestQuestions.AsNoTracking()
            .Where(q => q.TestId == assignment.TestId).OrderBy(q => q.Sorting).ToListAsync(cancellationToken);
        var questionIds = questions.Select(q => q.Id).ToList();
        var options = await db.BewerbungTestOptions.AsNoTracking()
            .Where(o => questionIds.Contains(o.QuestionId)).OrderBy(o => o.Sorting).ToListAsync(cancellationToken);

        var qViews = questions.Select(q => new TestQuestionView(
            q.Id, q.Type, q.Prompt, q.Required,
            options.Where(o => o.QuestionId == q.Id).Select(o => new TestOptionView(o.Id, o.Label)).ToList()))
            .ToList();

        return new TestView(assignment.Id, test.Title, test.Description, assignment.CompletedAt is not null, qViews);
    }

    public async Task SubmitAnswersAsync(string assignmentId, IReadOnlyList<TestAnswerInput> answers, ClaimsPrincipal applicant, CancellationToken cancellationToken = default)
    {
        Permission.RequireApplicant(applicant);
        var userId = applicant.GetAgentId();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var assignment = await db.BewerbungTestAssignments.FirstOrDefaultAsync(a => a.Id == assignmentId, cancellationToken)
            ?? throw new InvalidOperationException("Testzuweisung nicht gefunden.");
        var bewerbung = await db.Bewerbungen.FirstOrDefaultAsync(b => b.Id == assignment.BewerbungId, cancellationToken)
            ?? throw new InvalidOperationException("Bewerbung nicht gefunden.");
        if (bewerbung.ApplicantUserId != userId)
        {
            throw new UnauthorizedAccessException("Das ist nicht dein Test.");
        }
        if (assignment.CompletedAt is not null)
        {
            throw new InvalidOperationException("Dieser Test wurde bereits abgeschlossen.");
        }

        foreach (var input in answers)
        {
            db.BewerbungTestAnswers.Add(new BewerbungTestAnswer
            {
                AssignmentId = assignment.Id,
                QuestionId = input.QuestionId,
                SelectedOptionId = string.IsNullOrWhiteSpace(input.SelectedOptionId) ? null : input.SelectedOptionId,
                FreeTextAnswer = Trim(input.FreeText),
            });
        }
        assignment.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        broadcaster.Report(bewerbung.Id);

        try
        {
            var recipient = bewerbung.AssignedAgentId;
            if (!string.IsNullOrEmpty(recipient))
            {
                await notifications.NotifyAsync(recipient, NotificationType.Recruiting,
                    $"Test abgeschlossen ({bewerbung.CaseNumber})", $"/bewerbungen/{bewerbung.Id}", cancellationToken);
            }
        }
        catch { /* best effort */ }
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
