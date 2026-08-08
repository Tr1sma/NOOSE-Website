using System.Security.Claims;

namespace NOOSE_Website.Services;

/// <summary>Recruiting auto-provisioning config: whether HRB self-assignment creates a case + document, and from which template.</summary>
public interface IRecruitingAutomationService
{
    /// <summary>Current config (cached ~10 s); defaults to enabled + the seeded template.</summary>
    Task<RecruitingAutomationConfig> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Persists the toggle + template choice. HRB or leadership only.</summary>
    Task SaveAsync(RecruitingAutomationConfig input, ClaimsPrincipal actor, CancellationToken cancellationToken = default);
}

/// <summary>Auto-case toggle + the document template used for the "Sicherheitsüberprüfung" document.</summary>
public sealed record RecruitingAutomationConfig(bool AutoCaseEnabled, string TemplateId);
