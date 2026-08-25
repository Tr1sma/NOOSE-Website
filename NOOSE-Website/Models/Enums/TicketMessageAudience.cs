namespace NOOSE_Website.Models.Enums;

/// <summary>Who a ticket message is visible to.</summary>
public enum TicketMessageAudience
{
    /// <summary>Internal handling notes; never visible to the citizen.</summary>
    Intern = 0,
    /// <summary>Conversation shared with the citizen; the agency answers under a constant sender.</summary>
    Buerger = 1,
}
