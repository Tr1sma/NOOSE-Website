namespace NOOSE_Website.Models.Enums;

/// <summary>Highest due-date reminder already sent for a job; monotonic so each milestone fires once.</summary>
public enum JobDueReminderStage
{
    None = 0,
    ThreeDays = 1,
    OneDay = 2,
    DueDay = 3,
}
