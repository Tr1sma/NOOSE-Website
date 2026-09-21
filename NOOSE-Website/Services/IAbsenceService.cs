using System.Security.Claims;
using NOOSE_Website.Data.Entities.Absences;
using NOOSE_Website.Models.Absences;
using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Self-service absences over whole days, plus the leadership overview and acknowledgement.</summary>
public interface IAbsenceService
{
    /// <summary>Absences visible to the caller, newest first; an optional day window filters by overlap.</summary>
    Task<List<AbsenceRow>> GetListAsync(ClaimsPrincipal viewer, AbsenceViewScope requested,
        DateOnly? from = null, DateOnly? to = null, CancellationToken cancellationToken = default);

    Task<Absence?> GetDetailAsync(string id, ClaimsPrincipal viewer, AbsenceViewScope requested,
        CancellationToken cancellationToken = default);

    /// <summary>Roster members signed off on a day, each with the day their absence ends.</summary>
    /// <remarks>
    /// For the agent pickers, so a task is not quietly hung on somebody who is away for a fortnight. It carries
    /// the end day and nothing else: the roster tier of <see cref="AbsenceVisibility"/> grants colleagues the row,
    /// never the reason. Overlapping sign-offs collapse to the one that ends last, which is the date a picker
    /// should show.
    /// </remarks>
    Task<IReadOnlyDictionary<string, DateOnly>> GetAbsentOnAsync(DateOnly day, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    Task<List<Absence>> GetTrashAsync(CancellationToken cancellationToken = default);

    Task<Absence> CreateAsync(AbsenceInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task RefreshAsync(string id, AbsenceInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Leadership takes note; idempotent and never blocking.</summary>
    Task AcknowledgeAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
