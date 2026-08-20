namespace NOOSE_Website.Models.Enums;

/// <summary>Kind of citizen ticket.</summary>
/// <remarks>
/// One value on purpose. The column exists so that a second kind is an enum value rather than a migration; stocking it
/// with values nothing sets would be dead code behind the fallback arm.
/// </remarks>
public enum TicketArt
{
    Fuehrungsebene = 0,
}

/// <summary>Display labels.</summary>
public static class TicketArtDisplay
{
    public static string Name(TicketArt art) => art switch
    {
        TicketArt.Fuehrungsebene => "Führungsebene",
        _ => "—",
    };

    public static readonly IReadOnlyList<TicketArt> All = new[] { TicketArt.Fuehrungsebene };
}
