using NOOSE_Website.Models.Enums;

namespace NOOSE_Website.Services;

/// <summary>Sole source for which account states may hold a signed-in session.</summary>
/// <remarks>
/// The login endpoint and the circuit revalidation must agree: a status the endpoint signs in has to survive
/// the 30-second revalidation, or the account is signed out again seconds after it arrives. Applicants and
/// citizens are Discord-authenticated accounts without agent rights, not lesser agents.
/// </remarks>
public static class AgentStatusRules
{
    /// <summary>True when an account in this state may keep a session; anything else is evicted on the next tick.</summary>
    public static bool MayHoldSession(AgentStatus status)
        => status is AgentStatus.Active or AgentStatus.Applicant or AgentStatus.Civilian;
}
