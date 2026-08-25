namespace NOOSE_Website.Models.Enums;

/// <summary>Which slice of the ticket desk leadership is looking at.</summary>
public enum TicketInboxScope
{
    /// <summary>Untouched.</summary>
    Offen = 0,
    /// <summary>Assigned and being answered.</summary>
    Bearbeitung = 1,
    /// <summary>Answered; the citizen owes the next word.</summary>
    Wartet = 2,
    /// <summary>Closed.</summary>
    Geschlossen = 3,
}
