using MudBlazor;

namespace NOOSE_Website.Models.Enums;

/// <summary>Lifecycle of a job application.</summary>
public enum BewerbungStatus
{
    Eingereicht = 0,
    InSicherheitspruefung = 1,
    ImTest = 2,
    ImVorstellungsgespraech = 3,
    Angenommen = 4,
    Abgelehnt = 5,
    Geschlossen = 6,
}

/// <summary>Display labels and chip colors.</summary>
public static class BewerbungStatusDisplay
{
    public static string Name(BewerbungStatus status) => status switch
    {
        BewerbungStatus.Eingereicht => "Eingereicht",
        BewerbungStatus.InSicherheitspruefung => "Sicherheitsüberprüfung",
        BewerbungStatus.ImTest => "Test",
        BewerbungStatus.ImVorstellungsgespraech => "Vorstellungsgespräch",
        BewerbungStatus.Angenommen => "Angenommen",
        BewerbungStatus.Abgelehnt => "Abgelehnt",
        BewerbungStatus.Geschlossen => "Geschlossen",
        _ => "—",
    };

    public static Color ChipColor(BewerbungStatus status) => status switch
    {
        BewerbungStatus.Eingereicht => Color.Info,
        BewerbungStatus.InSicherheitspruefung => Color.Warning,
        BewerbungStatus.ImTest => Color.Warning,
        BewerbungStatus.ImVorstellungsgespraech => Color.Primary,
        BewerbungStatus.Angenommen => Color.Success,
        BewerbungStatus.Abgelehnt => Color.Error,
        BewerbungStatus.Geschlossen => Color.Default,
        _ => Color.Default,
    };

    /// <summary>Terminal states allow no further transitions.</summary>
    public static bool IsTerminal(BewerbungStatus status)
        => status is BewerbungStatus.Angenommen or BewerbungStatus.Abgelehnt or BewerbungStatus.Geschlossen;
}
