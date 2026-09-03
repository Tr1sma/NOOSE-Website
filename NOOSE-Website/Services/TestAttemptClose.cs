using Microsoft.EntityFrameworkCore;
using NOOSE_Website.Data;
using NOOSE_Website.Data.Entities.Recruiting;

namespace NOOSE_Website.Services;

/// <summary>The two database steps that close a test attempt, shared by the applicant path and the sweep.</summary>
/// <remarks>
/// Written once so the applicant's own hand-in, the read path's late close and the background sweep cannot end an
/// attempt in three subtly different states. The caller owns its guard, its audit row and its notifications.
/// </remarks>
internal static class TestAttemptClose
{
    /// <summary>Adds the empty answer row an untouched question has none of; draft rows are left alone.</summary>
    /// <remarks>The evaluation keys on one row per question, and a missing row reads as "question added later"
    /// rather than "left blank". Reusing the draft upsert with an empty payload would blank what was typed.</remarks>
    public static async Task FillMissingAnswersAsync(AppDbContext db, BewerbungTestAssignment assignment,
        CancellationToken cancellationToken)
    {
        var answered = await db.BewerbungTestAnswers.AsNoTracking()
            .Where(a => a.AssignmentId == assignment.Id)
            .Select(a => a.QuestionId).ToListAsync(cancellationToken);
        var missing = await db.BewerbungTestQuestions.AsNoTracking()
            .Where(q => q.TestId == assignment.TestId && !answered.Contains(q.Id))
            .Select(q => q.Id).ToListAsync(cancellationToken);
        foreach (var questionId in missing)
        {
            db.BewerbungTestAnswers.Add(new BewerbungTestAnswer
            {
                AssignmentId = assignment.Id,
                QuestionId = questionId,
            });
        }
    }

    /// <summary>Claims the close; exactly one of applicant, read path and sweep gets true.</summary>
    /// <remarks>CompletedAt is the idempotency token, so a second sweep selects nothing and no extra column
    /// is needed. ExecuteUpdate bypasses the interceptors, so the caller owes an audit row.</remarks>
    public static async Task<bool> ClaimAsync(AppDbContext db, string assignmentId, DateTime now, bool timedOut,
        CancellationToken cancellationToken)
        => await db.BewerbungTestAssignments
            .Where(a => a.Id == assignmentId && a.CompletedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.CompletedAt, now)
                .SetProperty(a => a.TimedOut, timedOut), cancellationToken) == 1;
}
