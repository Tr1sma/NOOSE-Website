namespace NOOSE_Website.Models.Enums;

/// <summary>Kind of ticket.</summary>
/// <remarks>
/// <see cref="Intern"/> has no citizen at all: agents used to take their own questions to leadership over Discord, and
/// that conversation belongs in the same desk as the citizen one — same thread, same status machine, no citizen side.
/// </remarks>
public enum TicketArt
{
    Fuehrungsebene = 0,

    /// <summary>Opened by an agent for the house; no citizen profile, no citizen thread, no module gate.</summary>
    Intern = 1,
}

/// <summary>Display labels.</summary>
public static class TicketArtDisplay
{
    public static string Name(TicketArt art) => art switch
    {
        TicketArt.Fuehrungsebene => "Führungsebene",
        TicketArt.Intern => "Intern",
        _ => "—",
    };

    public static readonly IReadOnlyList<TicketArt> All = new[] { TicketArt.Fuehrungsebene, TicketArt.Intern };
}
