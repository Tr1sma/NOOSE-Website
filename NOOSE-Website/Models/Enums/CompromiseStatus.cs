using MudBlazor;

namespace NOOSE_Website.Models.Enums;

/// <summary>Whether a record compromised by an abduction is still exposed or has been re-classified as normal.</summary>
public enum CompromiseStatus
{
    Compromised = 0,
    Cleared = 1,
}

/// <summary>Display labels and chip colors.</summary>
public static class CompromiseStatusDisplay
{
    public static string Name(CompromiseStatus status) => status switch
    {
        CompromiseStatus.Compromised => "Kompromittiert",
        CompromiseStatus.Cleared => "Wieder eingestuft",
        _ => "—",
    };

    public static Color ChipColor(CompromiseStatus status) => status switch
    {
        CompromiseStatus.Compromised => Color.Error,
        CompromiseStatus.Cleared => Color.Success,
        _ => Color.Default,
    };
}
