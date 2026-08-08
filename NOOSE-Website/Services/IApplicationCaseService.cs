using System.Security.Claims;

namespace NOOSE_Website.Services;

/// <summary>Auto-provisions the "Bewerbungsverfahren" case + "Sicherheitsüberprüfung" document for an application.</summary>
public interface IApplicationCaseService
{
    /// <summary>Idempotently create the case + document (from the configured template) and attach them; no-op when disabled or already provisioned.</summary>
    Task EnsureSecurityCheckCaseAsync(string bewerbungId, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}
