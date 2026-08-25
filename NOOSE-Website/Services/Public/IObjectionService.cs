using System.Security.Claims;
using NOOSE_Website.Data.Entities.Public;
using NOOSE_Website.Models.Enums;
using NOOSE_Website.Models.Public;

namespace NOOSE_Website.Services.Public;

/// <summary>Citizen objections to public wanted notices: filing, the desk, and the one decision.</summary>
/// <remarks>
/// Two audiences, one table, the split <see cref="ITipService"/> and <see cref="ITicketService"/> already draw: a
/// citizen addresses an objection by case number and gets a <c>CitizenObjection*</c> record that structurally holds
/// no agent, while the desk addresses it by row id. A raw row id from outside would be an existence oracle.
/// </remarks>
public interface IObjectionService
{
    // ---- citizen ----

    /// <summary>Files an objection against a published notice and returns its case number.</summary>
    Task<string> SubmitAsync(ObjectionInput input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>The caller's own objections, newest first; empty for an account without a civilian profile.</summary>
    Task<IReadOnlyList<CitizenObjectionRow>> GetOwnAsync(ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    // ---- desk ----

    Task<IReadOnlyList<ObjectionRow>> GetListAsync(bool onlyOpen, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    Task<ObjectionCounts> GetCountsAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    Task<ObjectionDetail?> GetAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Moves the objection along; a decision needs a note, and upholding one needs the notice offline.</summary>
    Task SetStatusAsync(string id, ObjectionStatus status, string? note, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    /// <summary>Opens a case over the objection and remembers it; returns the new case id.</summary>
    Task<string> ToCaseAsync(string id, string? title, ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    // ---- trash ----

    Task<List<FahndungEinspruch>> GetTrashAsync(CancellationToken cancellationToken = default);

    Task RestoreAsync(string id, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
