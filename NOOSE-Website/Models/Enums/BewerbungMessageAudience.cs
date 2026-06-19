namespace NOOSE_Website.Models.Enums;

/// <summary>Who a recruiting message is visible to.</summary>
public enum BewerbungMessageAudience
{
    /// <summary>HRB-internal discussion thread; never visible to the applicant.</summary>
    Intern = 0,
    /// <summary>Conversation shared with the applicant.</summary>
    Bewerber = 1,
}
