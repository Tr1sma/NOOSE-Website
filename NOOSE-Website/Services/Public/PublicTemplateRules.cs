namespace NOOSE_Website.Services.Public;

/// <summary>Limits of a public template; read by the service and by the editor dialog alike.</summary>
public static class PublicTemplateRules
{
    public const int TitleMinLength = 3;
    public const int TitleMaxLength = 160;

    /// <summary>Below this it is not a template, it is a greeting.</summary>
    public const int MinLength = 20;

    /// <summary>Room the substitutions may still take on top of the stored text.</summary>
    /// <remarks>
    /// A flat reserve is a guess, not a bound: BUERGER alone grows by up to 122 characters per occurrence (a name
    /// is two 64-character halves), so a template made of those exceeds the message cap however generous the
    /// reserve. It stays as the cheap first check; <see cref="MaxRenderedLength"/> is the real one, verified
    /// against a worst-case render at save time.
    /// </remarks>
    private const int SubstitutionReserve = 200;

    /// <summary>Cheap first bound on the STORED text.</summary>
    public const int MaxLength = TicketRules.MaxMessageLength - SubstitutionReserve;

    /// <summary>What the RENDERED text has to fit into: the message it becomes.</summary>
    public const int MaxRenderedLength = TicketRules.MaxMessageLength;
}
