namespace NOOSE_Website.Models.Enums;

/// <summary>Which citizen-facing message a public template is written for.</summary>
/// <remarks>
/// Every value has a place that applies it. No spare values: a kind nothing reads is a template an editor writes and
/// no citizen ever receives. A reward promise and a press release were considered and left out — the payout writes no
/// citizen message at all today, and the press draft belongs to the phase that builds it.
/// </remarks>
public enum PublicTemplateKind
{
    /// <summary>Automatic confirmation written when a citizen opens a ticket.</summary>
    TicketEingang = 0,

    /// <summary>Offered in the reply field of the ticket desk.</summary>
    TicketAntwort = 1,

    /// <summary>Automatic confirmation written when a citizen files a tip.</summary>
    HinweisEingang = 2,

    /// <summary>Offered when the desk asks a tipster back.</summary>
    HinweisRueckfrage = 3,

    /// <summary>Offered when a tip is turned down.</summary>
    HinweisAblehnung = 4,
}

/// <summary>Display labels.</summary>
public static class PublicTemplateKindDisplay
{
    public static string Name(PublicTemplateKind kind) => kind switch
    {
        PublicTemplateKind.TicketEingang => "Ticket – Eingangsbestätigung",
        PublicTemplateKind.TicketAntwort => "Ticket – Antwort",
        PublicTemplateKind.HinweisEingang => "Hinweis – Eingangsbestätigung",
        PublicTemplateKind.HinweisRueckfrage => "Hinweis – Rückfrage",
        PublicTemplateKind.HinweisAblehnung => "Hinweis – Ablehnung",
        _ => "—",
    };

    /// <summary>What the editor is told about when this template goes out.</summary>
    public static string Hint(PublicTemplateKind kind) => kind switch
    {
        PublicTemplateKind.TicketEingang => "Wird automatisch gesendet, sobald ein Bürger ein Ticket öffnet.",
        PublicTemplateKind.TicketAntwort => "Steht der Führung im Antwortfeld eines Tickets zur Auswahl.",
        PublicTemplateKind.HinweisEingang => "Wird automatisch gesendet, sobald ein Bürger einen Hinweis einreicht.",
        PublicTemplateKind.HinweisRueckfrage => "Steht im Schriftwechsel mit dem Hinweisgeber zur Auswahl.",
        PublicTemplateKind.HinweisAblehnung => "Steht im Schriftwechsel mit dem Hinweisgeber zur Auswahl.",
        _ => string.Empty,
    };

    /// <summary>True when this kind is sent without an agent picking it.</summary>
    public static bool IsAutomatic(PublicTemplateKind kind)
        => kind is PublicTemplateKind.TicketEingang or PublicTemplateKind.HinweisEingang;

    public static readonly IReadOnlyList<PublicTemplateKind> All = new[]
    {
        PublicTemplateKind.TicketEingang,
        PublicTemplateKind.TicketAntwort,
        PublicTemplateKind.HinweisEingang,
        PublicTemplateKind.HinweisRueckfrage,
        PublicTemplateKind.HinweisAblehnung,
    };
}
