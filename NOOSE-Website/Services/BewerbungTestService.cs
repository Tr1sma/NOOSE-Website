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
        // an open assignment points at this test, and there is no way to un-assign one: deleting it left the
        // applicant unable to finish and the HRB unable to decide, with no route out for either
        if (await db.BewerbungTestAssignments
                .AnyAsync(a => a.TestId == id && a.CompletedAt == null, cancellationToken))
        {
            throw new InvalidOperationException("Dieser Test ist noch zugewiesen und nicht abgeschlossen. "
                + "Setze ihn auf inaktiv, damit er nicht mehr vergeben wird.");
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

    public async Task UpdateQuestionAsync(string questionId, string prompt, bool required, int points, bool? correctYesNo, string? keywords, int? minKeywordHits, bool keepOptionOrder, bool allowMultiple, ClaimsPrincipal actor, CancellationToken cancellationToken = default)
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
        question.KeepOptionOrder = question.Type == TestQuestionType.MultipleChoice && keepOptionOrder;
        question.AllowMultiple = question.Type == TestQuestionType.MultipleChoice && allowMultiple;
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
        return assignment is null ? null : await BuildEvaluationAsync(db, assignment, cancellationToken);
    }

    /// <summary>The evaluation itself; both grading transitions read it through the same path the panel does.</summary>
    private static async Task<TestEvaluation> BuildEvaluationAsync(
        AppDbContext db, BewerbungTestAssignment assignment, CancellationToken cancellationToken)
    {
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
                    var own = options.Where(o => o.QuestionId == q.Id).ToList();
                    var chosenIds = TestGrading.SplitOptionIds(answer?.SelectedOptionIds, answer?.SelectedOptionId);
                    var chosen = own.Where(o => chosenIds.Contains(o.Id, StringComparer.Ordinal)).ToList();
                    answerText = chosen.Count == 0 ? null : string.Join(", ", chosen.Select(o => o.Label));
                    // the SHAPE OF THE STORED ANSWER decides the overload, not the question's current switch:
                    // turning Mehrfachauswahl off after the submission would otherwise grade a two-tick answer on
                    // whichever single option happened to come first
                    autoCorrect = q.AllowMultiple || chosenIds.Count > 1
                        ? TestGrading.GradeMultipleChoice(chosenIds, own)
                        : TestGrading.GradeMultipleChoice(chosen.FirstOrDefault());
                    if (!own.Any(o => o.IsCorrect))
                    {
                        // no option flagged: the machine cannot decide this one, and "wrong" would let
                        // CompleteGradingAsync freeze a 0 that nobody ever looked at
                        autoCorrect = null;
                    }
                    var correct = own.Where(o => o.IsCorrect).Select(o => o.Label).ToList();
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
            // clamped on read as well as on write: the column is data, not a promise
            var awarded = answer?.ManualPoints is int manual
                ? Math.Clamp(manual, 0, q.Points)
                : effective == true ? q.Points : 0;
            total += awarded;

            items.Add(new TestEvaluationItem(
                answer?.Id, q.Id, q.Type, q.Prompt, answerText,
                autoCorrect, answer?.ManualCorrect, answer?.ManualPoints, effective,
                q.Points, awarded, correctAnswer, matched, missed));
        }

        // once grading is closed the headline figures are the frozen ones: editing the test afterwards must not
        // rewrite the verdict of an applicant who was already told the outcome
        return new TestEvaluation(
            test?.Title ?? "Test", assignment.CompletedAt,
            assignment.GradedAt is null ? total : assignment.FinalPoints ?? total,
            assignment.GradedAt is null ? max : assignment.FinalMaxPoints ?? max,
            // the threshold is frozen too: Passed is derived from it, so a later edit to the Bestehensgrenze would
            // otherwise flip the verdict of an applicant who was already told the outcome
            assignment.GradedAt is null ? test?.PassPercent : assignment.FinalPassPercent,
            assignment.GradedAt, assignment.GradedByName, items);
    }

    public async Task SetAwardedPointsAsync(string assignmentId, string questionId, int? points,
        ClaimsPrincipal actor, CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadership(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var assignment = await db.BewerbungTestAssignments.FirstOrDefaultAsync(a => a.Id == assignmentId, cancellationToken)
            ?? throw new InvalidOperationException("Testzuweisung nicht gefunden.");
        if (assignment.GradedAt is not null)
        {
            throw new InvalidOperationException("Die Korrektur ist abgeschlossen. Bitte sie zuerst wieder öffnen.");
        }
        var question = await db.BewerbungTestQuestions.AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == questionId && q.TestId == assignment.TestId, cancellationToken)
            ?? throw new InvalidOperationException("Frage gehört nicht zu diesem Test.");

        // keyed on the question, not on an answer row: a question added after the applicant submitted has none
        var answer = await db.BewerbungTestAnswers
            .FirstOrDefaultAsync(a => a.AssignmentId == assignmentId && a.QuestionId == questionId, cancellationToken);
        if (answer is null)
        {
            answer = new BewerbungTestAnswer { AssignmentId = assignmentId, QuestionId = questionId };
            db.BewerbungTestAnswers.Add(answer);
        }
        answer.ManualPoints = points is null ? null : Math.Clamp(points.Value, 0, question.Points);
        // "Auto" clears the old verdict too, otherwise a row graded before points existed would keep overriding
        if (points is null)
        {
            answer.ManualCorrect = null;
        }
        await db.SaveChangesAsync(cancellationToken);

        broadcaster.Report(assignment.BewerbungId);
    }

    public async Task CompleteGradingAsync(string bewerbungId, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadership(actor);
        var evaluation = await GetEvaluationInternalAsync(bewerbungId, cancellationToken)
            ?? throw new InvalidOperationException("Kein Test zugewiesen.");
        if (evaluation.Assignment.CompletedAt is null)
        {
            throw new InvalidOperationException("Der Bewerber hat den Test noch nicht abgegeben.");
        }
        if (evaluation.Report.OpenCount > 0)
        {
            throw new InvalidOperationException(
                $"Noch {evaluation.Report.OpenCount} Frage(n) ohne Wertung. Bitte zuerst alle bewerten.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var assignment = await db.BewerbungTestAssignments
            .FirstOrDefaultAsync(a => a.Id == evaluation.Assignment.Id, cancellationToken)
            ?? throw new InvalidOperationException("Testzuweisung nicht gefunden.");
        assignment.GradedAt = DateTime.UtcNow;
        assignment.GradedByName = actor.GetCodename();
        assignment.FinalPoints = evaluation.Report.TotalPoints;
        assignment.FinalMaxPoints = evaluation.Report.MaxPoints;
        assignment.FinalPassPercent = evaluation.Report.PassPercent;
        await db.SaveChangesAsync(cancellationToken);

        broadcaster.Report(bewerbungId);

        try
        {
            var recipient = await db.Bewerbungen.AsNoTracking()
                .Where(b => b.Id == bewerbungId).Select(b => b.AssignedAgentId).FirstOrDefaultAsync(cancellationToken);
            if (!string.IsNullOrEmpty(recipient))
            {
                await notifications.NotifyAsync(recipient, NotificationType.Recruiting,
                    "Test korrigiert", $"/bewerbungen/{bewerbungId}", cancellationToken);
            }
        }
        catch { /* best effort */ }
    }

    public async Task ReopenGradingAsync(string bewerbungId, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        Permission.RequireHrbOrLeadership(actor);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var assignment = await db.BewerbungTestAssignments
            .FirstOrDefaultAsync(a => a.BewerbungId == bewerbungId, cancellationToken)
            ?? throw new InvalidOperationException("Testzuweisung nicht gefunden.");
        if (assignment.GradedAt is null)
        {
            return;
        }
        // the frozen figures go with it: leaving them would show a stale result next to live per-question points
        assignment.GradedAt = null;
        assignment.GradedByName = null;
        assignment.FinalPoints = null;
        assignment.FinalMaxPoints = null;
        assignment.FinalPassPercent = null;
        await db.SaveChangesAsync(cancellationToken);

        broadcaster.Report(bewerbungId);
    }

    /// <summary>Evaluation plus its assignment, for the two grading transitions; the guard is the caller's.</summary>
    private async Task<(BewerbungTestAssignment Assignment, TestEvaluation Report)?> GetEvaluationInternalAsync(
        string bewerbungId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var assignment = await db.BewerbungTestAssignments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.BewerbungId == bewerbungId, cancellationToken);
        if (assignment is null)
        {
            return null;
        }
        var report = await BuildEvaluationAsync(db, assignment, cancellationToken);
        return (assignment, report);
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

        var own = await db.Bewerbungen.AsNoTracking()
            .Where(b => b.ApplicantUserId == userId)
            .OrderByDescending(b => b.SubmittedAt)
            .Select(b => new { b.Id, b.CaseNumber }).FirstOrDefaultAsync(cancellationToken);
        if (own is null)
        {
            return null;
        }
        var bewerbungId = own.Id;
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
            q.Id, q.Type, q.Prompt, q.Required, q.AllowMultiple,
            TestOptionOrder.For(assignment.Id, q.KeepOptionOrder, options.Where(o => o.QuestionId == q.Id))
                .Select(o => new TestOptionView(o.Id, o.Label)).ToList()))
            .ToList();

        return new TestView(assignment.Id, own.CaseNumber, test.Title, test.Description, assignment.CompletedAt is not null, qViews);
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
        // and not after the application itself is decided: answers arriving then change nothing and read as if
        // the decision were still open
        if (BewerbungStatusDisplay.IsTerminal(bewerbung.Status))
        {
            throw new InvalidOperationException("Über diese Bewerbung ist bereits entschieden.");
        }

        // the payload is never trusted: a foreign question id, an option that belongs to another question, a
        // duplicate row and a skipped mandatory question all have to die here, not only in the form. The column
        // carries no foreign key, so nothing downstream would catch an invented option id.
        var questions = await db.BewerbungTestQuestions.AsNoTracking()
            .Where(q => q.TestId == assignment.TestId)
            .Select(q => new { q.Id, q.Type, q.Required, q.AllowMultiple })
            .ToListAsync(cancellationToken);
        var questionIds = questions.Select(q => q.Id).ToList();
        var options = await db.BewerbungTestOptions.AsNoTracking()
            .Where(o => questionIds.Contains(o.QuestionId))
            .Select(o => new { o.Id, o.QuestionId })
            .ToListAsync(cancellationToken);

        // last wins, so a replayed payload cannot produce two rows for one question
        var submitted = answers
            .Where(a => !string.IsNullOrWhiteSpace(a.QuestionId))
            .GroupBy(a => a.QuestionId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);

        foreach (var question in questions)
        {
            submitted.TryGetValue(question.Id, out var input);
            var choice = new List<string>();
            string? freeText = null;

            if (question.Type == TestQuestionType.MultipleChoice)
            {
                var allowed = options.Where(o => o.QuestionId == question.Id)
                    .Select(o => o.Id).ToHashSet(StringComparer.Ordinal);
                choice = (input?.SelectedOptionIds ?? [])
                    .Where(allowed.Contains)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                if (!question.AllowMultiple && choice.Count > 1)
                {
                    choice = [choice[0]];
                }
            }
            else
            {
                freeText = Trim(input?.FreeText);
            }

            if (question.Required && choice.Count == 0 && freeText is null)
            {
                throw new InvalidOperationException("Bitte beantworte alle Pflichtfragen.");
            }

            // a row per question even when it stays empty: the evaluation keys on it, and a missing row reads as
            // "question added later" rather than "left blank"
            db.BewerbungTestAnswers.Add(new BewerbungTestAnswer
            {
                AssignmentId = assignment.Id,
                QuestionId = question.Id,
                SelectedOptionId = choice.Count == 1 ? choice[0] : null,
                SelectedOptionIds = TestGrading.JoinOptionIds(choice),
                FreeTextAnswer = freeText,
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
