using System.Security.Claims;
using NOOSE_Website.Models.Recruiting;

namespace NOOSE_Website.Services;

/// <summary>Recruitment bans (temporary) and blacklist (permanent) per applicant; gates re-application.</summary>
public interface IBewerbungssperreService
{
    /// <summary>The applicant's active ban or blacklist, or null. Blacklist takes precedence. No actor guard.</summary>
    Task<BewerbungssperreInfo?> GetActiveAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>All active bans and blacklist entries, newest first. HRB/leadership only.</summary>
    Task<List<BewerbungssperreInfo>> ListActiveAsync(ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Impose (or refresh) the 14-day temporary ban after a rejection. HRB/leadership, write access.</summary>
    Task BanAsync(string agentId, string? bewerbungId, string? applicantName, string? reason, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Put the applicant on the permanent blacklist. HRB/leadership, write access.</summary>
    Task BlacklistAsync(string agentId, string? bewerbungId, string? applicantName, string? reason, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Change a temporary ban's end date (a past date effectively lifts it). HRB/leadership, write access.</summary>
    Task ShortenAsync(string sperreId, DateTime newUntil, ClaimsPrincipal actor, CancellationToken cancellationToken = default);

    /// <summary>Lift a ban or remove from the blacklist (soft-delete). HRB/leadership, write access.</summary>
    Task LiftAsync(string sperreId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
