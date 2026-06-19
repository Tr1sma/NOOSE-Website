using MudBlazor;

namespace NOOSE_Website.Models.Enums;

/// <summary>In-app notification type.</summary>
public enum NotificationType
{
    /// <summary>Request approved or rejected.</summary>
    RequestDecided = 0,

    /// <summary>User mentioned via @.</summary>
    Mention = 1,

    /// <summary>Account event occurred.</summary>
    Account = 2,

    /// <summary>Watched record changed.</summary>
    RecordModified = 3,

    /// <summary>Task assigned to user.</summary>
    JobAssigned = 4,

    /// <summary>Announcement published.</summary>
    Announcement = 5,

    /// <summary>Follow-up date due.</summary>
    Followup = 6,

    /// <summary>Appointment assigned.</summary>
    AppointmentAssigned = 7,

    /// <summary>New situation report.</summary>
    SituationReport = 8,

    /// <summary>Recruiting/application event.</summary>
    Recruiting = 9,
}

/// <summary>Display labels and icons.</summary>
public static class NotificationTypeDisplay
{
    public static string Name(NotificationType type) => type switch
    {
        NotificationType.RequestDecided => "Antrag entschieden",
        NotificationType.Mention => "Erwähnung",
        NotificationType.Account => "Konto",
        NotificationType.RecordModified => "Beobachtete Akte geändert",
        NotificationType.JobAssigned => "Aufgabe",
        NotificationType.Announcement => "Ankündigung",
        NotificationType.Followup => "Wiedervorlage fällig",
        NotificationType.AppointmentAssigned => "Termin",
        NotificationType.SituationReport => "Lagebericht",
        NotificationType.Recruiting => "Bewerbung",
        _ => "Benachrichtigung",
    };

    /// <summary>Icon per notification type.</summary>
    public static string Icon(NotificationType type) => type switch
    {
        NotificationType.RequestDecided => Icons.Material.Filled.Gavel,
        NotificationType.Mention => Icons.Material.Filled.AlternateEmail,
        NotificationType.Account => Icons.Material.Filled.ManageAccounts,
        NotificationType.RecordModified => Icons.Material.Filled.Visibility,
        NotificationType.JobAssigned => Icons.Material.Filled.AssignmentInd,
        NotificationType.Announcement => Icons.Material.Filled.Campaign,
        NotificationType.Followup => Icons.Material.Filled.EventRepeat,
        NotificationType.AppointmentAssigned => Icons.Material.Filled.Event,
        NotificationType.SituationReport => Icons.Material.Filled.Assessment,
        NotificationType.Recruiting => Icons.Material.Filled.HowToReg,
        _ => Icons.Material.Filled.Notifications,
    };
}
