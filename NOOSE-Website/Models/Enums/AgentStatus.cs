namespace NOOSE_Website.Models.Enums;

/// <summary>Agent account lifecycle.</summary>
public enum AgentStatus
{
    Pending = 0,
    Active = 1,
    Blocked = 2,
    /// <summary>Public applicant; Discord-authenticated but not an agent, access limited to the applicant portal.</summary>
    Applicant = 3,
    /// <summary>Terminated agent; account denied and put on the recruitment blacklist.</summary>
    Terminated = 4,
    /// <summary>Public citizen; Discord-authenticated but not an agent, access limited to the public area.</summary>
    Civilian = 5,
}
