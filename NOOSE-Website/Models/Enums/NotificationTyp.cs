using MudBlazor;

namespace NOOSE_Website.Models.Enums;

/// <summary>
/// Art einer In-App-Benachrichtigung (Glocke). Bewusst erweiterbar – die Watchlist („gefolgte Akte geändert"),
/// Aufgaben-Zuteilungen und Behörden-Broadcasts kommen in späteren Phase-6-Teilprojekten hinzu.
/// </summary>
public enum NotificationTyp
{
    /// <summary>Ein vom Empfänger gestellter Antrag (z. B. Hochstufung) wurde genehmigt oder abgelehnt.</summary>
    AntragEntschieden = 0,

    /// <summary>Der Empfänger wurde in einem Vermerk/Kommentar oder Taskforce-Chat per @ erwähnt.</summary>
    Erwaehnung = 1,

    /// <summary>Ein Konto-Ereignis betrifft den Empfänger (Freigabe, Namensänderung genehmigt/abgelehnt).</summary>
    Konto = 2,

    /// <summary>Eine vom Empfänger beobachtete (gefolgte) Akte wurde geändert (Watchlist).</summary>
    AkteGeaendert = 3,
}

/// <summary>Anzeige-Helfer für <see cref="NotificationTyp"/> (UI-frei bis auf das Icon).</summary>
public static class NotificationTypAnzeige
{
    public static string Name(NotificationTyp typ) => typ switch
    {
        NotificationTyp.AntragEntschieden => "Antrag entschieden",
        NotificationTyp.Erwaehnung => "Erwähnung",
        NotificationTyp.Konto => "Konto",
        NotificationTyp.AkteGeaendert => "Beobachtete Akte geändert",
        _ => "Benachrichtigung",
    };

    /// <summary>Material-Icon je Typ für die Glocken-Liste.</summary>
    public static string Icon(NotificationTyp typ) => typ switch
    {
        NotificationTyp.AntragEntschieden => Icons.Material.Filled.Gavel,
        NotificationTyp.Erwaehnung => Icons.Material.Filled.AlternateEmail,
        NotificationTyp.Konto => Icons.Material.Filled.ManageAccounts,
        NotificationTyp.AkteGeaendert => Icons.Material.Filled.Visibility,
        _ => Icons.Material.Filled.Notifications,
    };
}
