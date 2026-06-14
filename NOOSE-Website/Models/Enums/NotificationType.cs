using MudBlazor;

namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Art einer In-App-Benachrichtigung (Glocke). Bewusst erweiterbar – die Watchlist („gefolgte Akte geändert"),
/// Aufgaben-Zuteilungen und Behörden-Broadcasts kommen in späteren Phase-6-Teilprojekten hinzu.
/// </summary>
public enum NotificationType
{
    /// <summary>Ein vom Empfänger gestellter Antrag (z. B. Hochstufung) wurde genehmigt oder abgelehnt.</summary>
    RequestDecided = 0,

    /// <summary>Der Empfänger wurde in einem Vermerk/Kommentar oder Taskforce-Chat per @ erwähnt.</summary>
    Mention = 1,

    /// <summary>Ein Konto-Ereignis betrifft den Empfänger (Freigabe, Namensänderung genehmigt/abgelehnt).</summary>
    Account = 2,

    /// <summary>Eine vom Empfänger beobachtete (gefolgte) Akte wurde geändert (Watchlist).</summary>
    RecordModified = 3,

    /// <summary>Dem Empfänger wurde eine Aufgabe zugewiesen (bzw. eine eigene Aufgabe wurde erledigt).</summary>
    JobAssigned = 4,

    /// <summary>Eine an den Empfänger gerichtete Ankündigung/Behörden-Broadcast wurde veröffentlicht.</summary>
    Announcement = 5,

    /// <summary>Eine Wiedervorlage an einer Akte ist fällig (Empfänger = Zuständiger oder Folger der Akte).</summary>
    Followup = 6,

    /// <summary>Dem Empfänger wurde ein Termin als Teilnehmer zugeteilt (Kalender, Phase 8 – Block C).</summary>
    AppointmentAssigned = 7,

    /// <summary>Ein neuer automatischer Monats-Lagebericht wurde erzeugt (Empfänger = Führung; Phase 8 – Block D).</summary>
    SituationReport = 8,
}

/// <summary>Anzeige-Helfer für <see cref="NotificationTyp"/> (UI-frei bis auf das Icon).</summary>
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
        _ => "Benachrichtigung",
    };

    /// <summary>Material-Icon je Typ für die Glocken-Liste.</summary>
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
        _ => Icons.Material.Filled.Notifications,
    };
}
