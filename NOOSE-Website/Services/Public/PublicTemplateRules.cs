namespace NOOSE_Website.Services.Public;

/// <summary>Limits of a public template; read by the service and by the editor dialog alike.</summary>
public static class PublicTemplateRules
{
    public const int TitleMinLength = 3;
    public const int TitleMaxLength = 160;

    /// <summary>Below this it is not a template, it is a greeting.</summary>
    public const int MinLength = 20;

    /// <summary>Room the substitutions may still take on top of the stored text.</summary>
    private const int SubstitutionReserve = 200;

    /// <summary>Derived from the message cap, never a second number: what is stored must still fit once rendered.</summary>
    public const int MaxLength = TicketRules.MaxMessageLength - SubstitutionReserve;
}
